using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using log4net;

namespace zeroconf.zeroconf
{
    /// <summary>
    /// This is the root class for the Service Discovery object.
    ///
    /// his class does not have any fancy hooks to clean up. The <see cref="Zeroconf.Dispose"/> method should be called when the
    /// class is to be discarded, but failing to do so won't break anything. Announced services will expire in
    /// their own time, which is typically two minutes - although during this time, conforming implementations
    /// should refuse to republish any duplicate services.
    /// </summary>
    public class Zeroconf : IDisposable
    {
        private static String DISCOVERY = "_services._dns-sd._upd.local";
        private static IPEndPoint BROADCAST4 = new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353);
        private static IPEndPoint BROADCAST6 = new IPEndPoint(IPAddress.Parse("FF02::FB"), 5353);
        private ListenerThread _thread;
        private List<Record> _registry;
        private List<Service> _services;
        private List<DiscoveredServices> _discoverers;
        private List<IPacketListener> _receiveListeners;
        private List<IPacketListener> _sendListeners;
        private bool _useIpv4 = true;
        private bool _useIpv6 = true;
        private String _hostname;
        private String _domain;
        private static ILog LOGGER = LogManager.GetLogger(typeof(Zeroconf));

        private static readonly ThreadLocal<Random> _local = new ThreadLocal<Random>(() =>
            new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))
        );

        /// <summary>
        /// Create a new Zeroconf object
        /// </summary>
        public Zeroconf()
        {
            SetDomain(".local");

            SetLocalHostName(GetOrCreateLocalHostName());
            
            _receiveListeners = new List<IPacketListener>();
            _sendListeners = new List<IPacketListener>();
            _discoverers = new List<DiscoveredServices>();
            _thread = new ListenerThread(this);
            _registry = new List<Record>();
            _services = new List<Service>();
        }

        public static String GetOrCreateLocalHostName()
        {
            String host = Dns.GetHostName();
            if (Equals(host, "localhost"))
            {
                host = Convert.ToBase64String(NextLongLocalThreadBytes()) + ".local";
                LOGGER.WarnFormat("Hostname cannot be 'localhost', temporary hostname is {0}", host);
                return host;
            }

            return host;
        }

        public Zeroconf SetUseIpv4(bool useIpv4)
        {
            _useIpv4 = useIpv4;
            return this;
        }

        public Zeroconf SetUseIpv6(bool useIpv6)
        {
            _useIpv6 = useIpv6;
            return this;
        }
        
        public void Dispose() {
            foreach (var service in new List<Service>(_services))
                Unannounce(service);
            
            _services.Clear();

            foreach (var discoverer in new List<DiscoveredServices>(_discoverers))
                discoverer.Stop();
            
            _discoverers.Clear();

            _thread.Dispose();
        }

        /// <summary>
        /// Add a <see cref="IPacketListener"/> to the list of listeners notified when a Service Discovery
        /// Packet is received
        /// </summary>
        /// <param name="listener">the listener</param>
        /// <returns>this Zeroconf</returns>
        public Zeroconf AddReceiveListener(IPacketListener listener)
        {
            if (!_receiveListeners.Contains(listener)) 
                _receiveListeners.Add(listener);
            return this;
        }

        /// <summary>
        /// Remove a previously added <see cref="IPacketListener"/> from the list of listeners notified when
        /// a Service Discovery Packet is received
        /// </summary>
        /// <param name="listener">the listener</param>
        /// <returns>this Zeroconf</returns>
        public Zeroconf RemoveReceiveListener(IPacketListener listener)
        {
            _receiveListeners.Remove(listener);
            return this;
        }
        
        /// <summary>
        /// Add a <see cref="IPacketListener"/> to the list of listeners notified when a Service
        /// Discovery Packet is sent
        /// </summary>
        /// <param name="listener">the listener</param>
        /// <returns>this Zeroconf</returns>
        public Zeroconf AddSendListener(IPacketListener listener)
        {
            _sendListeners.Add(listener);
            return this;
        }

        /// <summary>
        /// Remove a previously added <see cref="IPacketListener"/> frm the list of listeners notified
        /// when a Service Discovery Packet is sent
        /// </summary>
        /// <param name="listener">the listener</param>
        /// <returns>this Zeroconf</returns>
        public Zeroconf RemoveSendListener(IPacketListener listener)
        {
            _sendListeners.Remove(listener);
            return this;
        }

        /// <summary>
        /// Add a <see cref="NetworkInterface"/> to the list of interfaces that send and receive Service
        /// Discovery Packets. The interface should be up, should <see cref="NetworkInterface"/> support
        /// Multicast and not be a Loopback interface. However, adding a NetworkInterface that does
        /// not match this requirement will not throw an Exception - it will just be ignored, as will
        /// any attempt to add a NetworkInterface that has already been added.
        /// <br />
        /// All the interface's IP addresses will be added to the list of
        /// <see cref="GetLocalAddresses"/> local addresses.
        /// If the interfaces's addresses change, or the interface is otherwise modified in a
        /// significant way, then it should be removed and re-added to this object. This is not done
        /// automatically
        /// </summary>
        /// <param name="nic">a NetworkInterface</param>
        /// <returns>this Zeroconf</returns>
        public Zeroconf AddNetworkInterface(NetworkInterface nic)
        {
            _thread.AddNetworkInterface(nic);
            return this;
        }

        public Zeroconf AddNetworkInterfaces(List<NetworkInterface> nics)
        {
            foreach (NetworkInterface nic in nics) _thread.AddNetworkInterface(nic);
            return this;
        }

        /// <summary>
        /// Remove a <see cref="AddNetworkInterface"/> previously added from this
        /// object's list. The addresses that were part of the interface at the time it was added
        /// will be removed from the list of local addresses.
        /// </summary>
        /// <param name="nic">a NetworkInterface</param>
        /// <returns>this Zeroconf</returns>
        public Zeroconf RemoveNetworkInterface(NetworkInterface nic)
        {
            _thread.RemoveNetworkInterface(nic);
            return this;
        }

        /// <summary>
        /// A convenience method to add all local NetworkInterfaces - it simply runs
        /// <code>
        /// foreach (var e in NetworkInterface.GetAllNetworkInterfaces())
        ///     AddNetworkInterface(e);
        /// </code>
        /// </summary>
        /// <returns>this Zeroconf</returns>
        public Zeroconf AddAllNetworkInterfaces()
        {
            foreach (var e in NetworkInterface.GetAllNetworkInterfaces()) 
                AddNetworkInterface(e);
            
            return this;
        }

        /// <summary>
        /// Get the Service Discovery Domain, which is set by <see cref="SetDomain"/>. It defaults to ".local",
        /// but can be set by <see cref="SetDomain"/>
        /// </summary>
        /// <returns>the domain</returns>
        public String GetDomain()
        {
            return _domain;
        }

        /// <summary>
        /// Set the Service Discovery Domain
        /// </summary>
        /// <param name="domain">the domain</param>
        /// <returns>this Zeroconf</returns>
        public Zeroconf SetDomain(String domain)
        {
            _domain = domain;
            return this;
        }

        /// <summary>
        /// Get the local hostname, which defaults to <code>Dns.GetHostName()</code>
        /// </summary>
        /// <returns>the local host name</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public String GetLocalHostName()
        {
            if (_hostname == null) throw new InvalidOperationException("Hostname cannot be determined");
            return _hostname;
        }

        /// <summary>
        /// Set the local hostname, as returned by <see cref="GetOrCreateLocalHostName"/>
        /// </summary>
        /// <param name="name">the hostname, which should be undotted</param>
        /// <returns>this Zeroconf</returns>
        public Zeroconf SetLocalHostName(String name)
        {
            _hostname = name;
            return this;
        }

        /// <summary>
        /// Return a list of InetAddresses which the Zeroconf object considers to be "local". These
        /// are the all the addresses of all the <see cref="NetworkInterface"/> objects added to this
        /// object. The returned list is a copy, it can be modified and will not be updated
        /// by this object.
        /// </summary>
        /// <returns>a List of local <see cref="IPEndPoint"/> objects</returns>
        public List<IPAddress> GetLocalAddresses()
        {
            return _thread.GetLocalAddresses();
        }

        /// <summary>
        /// Send a packet
        /// </summary>
        /// <param name="packet">the packet to send</param>
        public void Send(Packet packet)
        {
            _thread.Push(packet);
        }

        /// <summary>
        /// Return the registry of records. This is the list of DNS records that we will
        /// automatically match any queries against. The returned list is live.
        /// </summary>
        public List<Record> GetRegistry()
        {
            return _registry;
        }

        /// <summary>
        /// Return the list of all Services that have been announced by this object.
        /// </summary>
        public List<Service> GetAnnouncedServices()
        {
            return new List<Service>(_services);
        }

        /// <summary>
        /// Given a query packet, trawl through our registry and try to find any records that
        /// match the queries. If there are any, send our own response packet.
        /// <br />
        /// This is largely derived from other implementations, but broadly the logic here is
        /// that questions are matched against records based on the "name" and "type" fields,
        /// where <see cref="DISCOVERY"/> and <see cref="Record.TYPE_ANY"/> are wildcards for those
        /// fields. Currently we match against all packet types - should these be just "PTR"
        /// records?
        /// <br />
        /// Once we have this list of matched records, we search this list for any PTR records
        /// and add any matching SRV or TXT records (RFC 6763 12.1). After that, we scan our
        /// updated list and add any A or AAAA records that match any SRV records (12.2).
        /// <br />
        /// At the end of all this, if we have at least one record, send it as a response
        /// </summary>
        /// <param name="packet"></param>
        private void HandlePacket(Packet packet)
        {
            Packet response = null;
            ISet<String> targets = null;
            foreach (Record question in packet.GetQuestions())
            {
                foreach (Record record in GetRegistry())
                {
                    if ((question.GetName().Equals(DISCOVERY) || question.GetName().Equals(record.GetName())) &&
                        (question.GetType() == record.GetType() || question.GetType() == Record.TYPE_ANY &&
                            record.GetType() != Record.TYPE_NSEC))
                    {
                        if (response == null)
                        {
                            response = new Packet(packet.GetId());
                            response.SetAuthoritative(true);
                        }
                        
                        response.AddAnswer(record);
                        if (record is RecordSRV)
                        {
                            if (targets == null) targets = new HashSet<String>();
                            targets.Add(((RecordSRV) record).GetTarget());
                        }
                    }
                }

                if (response != null && question.GetType() != Record.TYPE_ANY)
                {
                    // When including a DNS-SD Service Instance Enumeration or Selective
                    // Instance Enumeration (subtype) PTR record in a response packet, the
                    // server/responder SHOULD include the following additional records:
                    // o The SRV record(s) named in the PTR rdata.
                    // o The TXT record(s) named in the PTR rdata.
                    // o All address records (type "A" and "AAAA") named in the SRV rdata.
                    foreach (Record answer in response.GetAnswers())
                    {
                        if (answer.GetType() != Record.TYPE_PTR)
                            continue;

                        foreach (Record record in GetRegistry())
                        {
                            if (record.GetName().Equals(((RecordPTR) answer).GetValue())
                                && (record.GetType() == Record.TYPE_SRV || record.GetType() == Record.TYPE_TXT)) {
                                response.AddAdditional(record);
                                if (record is RecordSRV)
                                {
                                    if (targets == null) targets = new HashSet<String>();
                                    targets.Add(((RecordSRV)record).GetTarget());
                                }
                            }
                        }
                    }
                }
            }

            if (response != null)
            {
                // When including an SRV record in a response packet, the
                // server/responder SHOULD include the following additional records:
                // o All address records (type "A" and "AAAA") named in the SRV rdata.
                if (targets != null)
                {
                    foreach (String target in targets)
                    {
                        foreach (Record record in GetRegistry())
                        {
                            if (record.GetName().Equals(target) && (record.GetType() == Record.TYPE_A ||
                                                                    record.GetType() == Record.TYPE_AAAA))
                            {
                                response.AddAdditional(record);
                            }
                        }
                    }
                }

                Send(response);
            }
        }

        /// <summary>
        /// Create a background thread that continuously searches for the given service.
        /// </summary>
        /// <param name="service">the service name, eg "_http"</param>
        /// <param name="protocol">the protocol, eg "_tcp"</param>
        /// <param name="domain">the domain, eg ".local"</param>
        /// <returns>a list of discovered services</returns>
        public DiscoveredServices Discover(String service, String protocol, String domain)
        {
            DiscoveredServices discoverer = new DiscoveredServices("_" + service + "._" + protocol + domain, this);
            Thread discovererThread = new Thread(discoverer.Run);
            discovererThread.Name = "zeroconf-discover-" + service + "-" + protocol + "-" + domain;
            discovererThread.Start();
            _discoverers.Add(discoverer);
            return discoverer;
        }
        
        /// <summary>
        /// Probe for a ZeroConf service with the specified name and return true if a matching
        /// service is found.
        /// <br />
        /// The approach is borrowed from https://www.npmjs.com/package/bonjour - we send three
        /// broadcasts trying to match the service name, 250ms apart. If we receive no response,
        /// assume there is no service that matches
        /// <br />
        /// Note the approach here is the only example of where we send a query packet. It could
        /// be used as the basis for us acting as a service discovery client
        /// </summary>
        /// <param name="fqdn">the fully qualified service name, eg "My Web Service._http._tcp.local".</param>
        private bool Probe(String fqdn)
        {
            Packet probe = new Packet();
            probe.SetResponse(false);
            probe.AddQuestion(new RecordANY(fqdn));
            AtomicBoolean match = new AtomicBoolean();
            IPacketListener probeListener = new ProbePacketListener(packet =>
            {
                if (packet.IsResponse())
                {
                    foreach (Record r in packet.GetAnswers())
                    {
                        if (r.GetName().Equals(fqdn, StringComparison.OrdinalIgnoreCase))
                        {
                            lock (match)
                            {
                                match.Set(true);
                                Monitor.PulseAll(match);
                            }
                        }
                    }

                    foreach (Record r in packet.GetAdditionals())
                    {
                        if (r.GetName().Equals(fqdn, StringComparison.OrdinalIgnoreCase))
                        {
                            lock (match)
                            {
                                match.Set(true);
                                Monitor.PulseAll(match);
                            }
                        }
                    }
                }
            });

            AddReceiveListener(probeListener);
            for (int i = 0; i < 3 && !match.Get(); i++)
            {
                Send(probe);
                lock (match)
                {
                    Monitor.Wait(match, 250);
                }
            }

            RemoveReceiveListener(probeListener);
            return match.Get(); 
        }

        /// <summary>
        /// Announce the service - probe to see if it already exists and fail if it does, otherwise
        /// announce it
        /// </summary>
        public void Announce(Service service)
        {
            if (service.GetDomain() == null) service.SetDomain(GetDomain());
            if (service.GetHost() == null) service.SetHost(GetLocalHostName());
            if (!service.HasAddresses()) service.AddAddresses(GetLocalAddresses());

            Packet packet = service.GetPacket();
            if (Probe(service.GetInstanceName()))
                throw new InvalidOperationException("Service " + service.GetInstanceName() + " already on network");
            
            GetRegistry().AddRange(packet.GetAnswers());
            _services.Add(service);

            for (int i = 0; i < 3; i++)
            {
                Send(packet);
                
                Thread.Sleep(225);
            }
            
            LOGGER.InfoFormat("Announced {0}.", service);
        }

        /// <summary>
        /// Unannounce the service. Do this by re-announcing all our records but with a TTL of 0 to
        /// ensure they expire. Then remove from the registry.
        /// </summary>
        public void Unannounce(Service service)
        {
            Packet packet = service.GetPacket();
            GetRegistry().RemoveAll(answer => packet.GetAnswers().Contains(answer));
            foreach (Record r in packet.GetAnswers())
            {
                GetRegistry().Remove(r);
                r.SetTTL(0);
            }

            _services.Remove(service);

            for (int i = 0; i < 3; i++)
            {
                Send(packet);
                
                Thread.Sleep(125);
            }
            
            LOGGER.InfoFormat("Unnanounced {0}.", service);
        }
        
        private class ProbePacketListener :  IPacketListener {
            public delegate void Callback(Packet packet);
            
            private Callback _callback;

            public ProbePacketListener(Callback callback)
            {
                _callback = callback;
            }
            
            public void PacketEvent(Packet packet)
            {
                _callback(packet);
            }
        }

        public static byte[] NextLongLocalThreadBytes()
        {
            var buffer = new byte[8];
            _local.Value.NextBytes(buffer);
            return buffer;
        }

        public class DiscoveredServices
        {
            private String _serviceName;
            private IPacketListener _listener;
            private readonly HashSet<DiscoveredService> _services = new HashSet<DiscoveredService>();
            private readonly object _servicesLock = new object();
            private volatile bool _shouldStop = false;
            private int _nextInterval = 1000;
            private Zeroconf _zeroconf;

            internal DiscoveredServices(String serviceName, Zeroconf zeroconf)
            {
                _serviceName = serviceName;
                _zeroconf = zeroconf;
            }

            private void AddRelated(Record record)
            {
                if (!record.GetName().EndsWith(_serviceName))
                    return;

                lock (_servicesLock)
                {
                    foreach (var s in _services)
                    {
                        if (String.Equals(s.ServiceName, record.GetName(), StringComparison.Ordinal))
                        {
                            s.AddRelatedRecord(record);
                        }
                    }
                }
            }

            private void AddService(RecordSRV record)
            {
                if (!record.GetName().EndsWith(_serviceName))
                    return;

                lock (_servicesLock)
                {
                    _services.RemoveWhere(s => s.IsExpired() || s.ServiceName.Equals(record.GetName()));
                    _services.Add(new DiscoveredService(record));
                }
            }

            public void Stop()
            {
                _shouldStop = true;
            }

            public List<DiscoveredService> GetServices()
            {
                return new List<DiscoveredService>(_services);
            }

            public DiscoveredService GetService(String name)
            {
                lock (_servicesLock)
                {
                    foreach (var service in _services)
                        if (service.Name.Equals(name))
                            return service;
                }

                return null;
            }
            
            public void Run()
            {
                while (!_shouldStop)
                {
                    Packet probe = new Packet();
                    probe.SetResponse(false);
                    probe.AddQuestion(new RecordPTR(_serviceName));
                    _zeroconf.Send(probe);
                    
                    Thread.Sleep(_nextInterval);
                    _nextInterval *= 2;
                    if (_nextInterval >= TimeSpan.FromMinutes(60).TotalMilliseconds)
                        _nextInterval = (int)(TimeSpan.FromMinutes(60).TotalMilliseconds + 20 + new Random().NextDouble() * 100);
                }
                
                _zeroconf.RemoveReceiveListener(_listener);
            }
        }
        
        private class ListenerThread : IDisposable
        {
            private readonly LinkedList<Packet> _sendq;
            private readonly Dictionary<NetworkInterface, Socket> _channels;
            private readonly Dictionary<NetworkInterface, List<IPAddress>> _localAddresses;
            private readonly ABLock _selectorLock = new ABLock();
            private volatile bool _cancelled;
            private Zeroconf _zeroconf;
            private Selector _selector;
            public Thread Thread { get; }

            internal ListenerThread(Zeroconf zeroconf)
            {
                _zeroconf = zeroconf;
                Thread = new Thread(Run);
                Thread.Name = "zeroconf-io-thread";
                _sendq = new LinkedList<Packet>();
                _channels = new Dictionary<NetworkInterface, Socket>();
                _localAddresses = new Dictionary<NetworkInterface, List<IPAddress>>();
            }
            
            private Selector GetSelector()
            {
                lock (_selectorLock)
                {
                    if (_selector == null)
                        _selector = new Selector();

                    return _selector;
                }
            }

            /// <summary>
            /// Stop the thread an rejoin
            /// </summary>
            public void Dispose()
            {
                _cancelled = true;
                if (_selector != null)
                {
                    _selector.Wakeup();
                    if (Thread.IsAlive) 
                        Thread.Join();
                }
            }

            /// <summary>
            /// Add a packet to the send queue
            /// </summary>
            internal void Push(Packet packet)
            {
                _sendq.AddLast(packet);
                if (_selector != null)
                {
                    // Only send if we have a Nic
                    _selector.Wakeup();
                }
            }

            /// <summary>
            /// Pop a packet from the send queue or return null if none available
            /// </summary>
            private Packet Pop()
            {
                Packet first = _sendq.First.Value;
                _sendq.RemoveFirst();
                return first;
            }

            /// <summary>
            /// Add a NetworkInterface. Try to identify whether it's IPV4 or IPV6, or both. IPV4 tested,
            /// IPV6 is not but at least it doesn't crash
            /// </summary>
            public void AddNetworkInterface(NetworkInterface nic)
            {
                if (!_channels.ContainsKey(nic) &&
                    nic.SupportsMulticast &&
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    bool ipv4 = false;
                    bool ipv6 = false;
                    List<IPAddress> locallist = new List<IPAddress>();

                    foreach (var addrInfo in nic.GetIPProperties().UnicastAddresses)
                    {
                        IPAddress a = addrInfo.Address;

                        if ((a.AddressFamily == AddressFamily.InterNetwork && !_zeroconf._useIpv4) ||
                            (a.AddressFamily == AddressFamily.InterNetworkV6 && !_zeroconf._useIpv6))
                            continue;

                        ipv4 |= a.AddressFamily == AddressFamily.InterNetwork;
                        ipv6 |= a.AddressFamily == AddressFamily.InterNetworkV6;

                        if (!IPAddress.IsLoopback(a) && !IsMulticast(a))
                            locallist.Add(a);
                    }

                    Socket channel = null;

                    if (ipv4)
                    {
                        channel = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        channel.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        channel.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);
                        channel.Bind(new IPEndPoint(IPAddress.Any, BROADCAST4.Port));
                        
                        IPAddress localIPv4 = nic.GetIPProperties().UnicastAddresses
                            .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;

                        if (localIPv4 != null)
                        {
                            channel.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
                                localIPv4.GetAddressBytes());
                            channel.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                                new MulticastOption(BROADCAST4.Address, localIPv4));
                        }
                    }
                    else if (ipv6)
                    {
                        channel = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                        channel.Bind(new IPEndPoint(IPAddress.IPv6Any, BROADCAST6.Port));

                        int ifIndex = nic.GetIPProperties().GetIPv6Properties().Index;
                        channel.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership,
                            new IPv6MulticastOption(BROADCAST6.Address, ifIndex));
                    }

                    if (channel != null)
                        channel.Blocking = false;
                    
                    _selectorLock.LockA1();
                    try
                    {
                        lock (_selector)
                        {
                            Monitor.PulseAll(_selector);
                        }

                        _selectorLock.LockA2();
                        try
                        {
                            _channels[nic] = channel;
                        }
                        finally
                        {
                            _selectorLock.UnlockA2();
                        }
                    }
                    finally
                    {
                        _selectorLock.UnlockA1();
                    }

                    _localAddresses.Add(nic, locallist);

                    if (!Thread.IsAlive)
                        Thread.Start();
                }
            }

            internal void RemoveNetworkInterface(NetworkInterface nic)
            {
                if (_channels.TryGetValue(nic, out Socket sock))
                {
                    _channels.Remove(nic);
                    _localAddresses.Remove(nic);

                    try
                    {
                        sock.Close();
                    }
                    catch
                    {
                    }
                }
            }

            internal List<IPAddress> GetLocalAddresses()
            {
                List<IPAddress> list = new List<IPAddress>();
                foreach (List<IPAddress> pernic in _localAddresses.Values)
                {
                    foreach (IPAddress address in pernic)
                    {
                        if (!list.Contains(address))
                            list.Add(address);
                    }
                }

                return list;
            }

            private bool IsMulticast(IPAddress ip)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte firstOctet = ip.GetAddressBytes()[0];
                    return firstOctet >= 224 && firstOctet <= 239;
                }
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    return ip.IsIPv6Multicast;
                }
                return false;
            }

            public void Run()
            {
                byte[] buffer = new byte[65536];
                BinaryWriter writer = new BinaryWriter(new MemoryStream(buffer));
                while (!_cancelled)
                {
                    writer.Clear(); 
                    try
                    {
                        Packet packet = Pop();
                        if (packet != null)
                        {
                            // Packet to Send
                            writer.Clear();
                            packet.Write(writer);
                            writer.BaseStream.Position = 0; 

                            foreach (var listener in _zeroconf._sendListeners)
                                listener.PacketEvent(packet);

                            foreach (var kv in _channels)
                            {
                                Socket channel = kv.Value;
                                EndPoint endpoint = packet.GetAddress() ??
                                                    (channel.AddressFamily == AddressFamily.InterNetwork
                                                        ? BROADCAST4
                                                        : BROADCAST6);
                                if (endpoint != null)
                                    channel.SendTo(buffer, 0,  (int) writer.BaseStream.Length, SocketFlags.None,
                                        endpoint);
                            }
                        }
                        
                        List<Socket> readList = _channels.Values.ToList();
                        Socket.Select(readList, null, null, 1000);

                        foreach (var channel in readList)
                        {
                            EndPoint remoteEP = new IPEndPoint(
                                channel.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any,
                                0);

                            int received = channel.ReceiveFrom(buffer, ref remoteEP);
                            if (received > 0)
                            {
                                writer.BaseStream.SetLength(received);
                                writer.BaseStream.Position = 0;
                                Packet incoming = new Packet();
                                incoming.Read(new BinaryReader(writer.BaseStream), remoteEP as IPEndPoint);

                                foreach (var listener in _zeroconf._receiveListeners)
                                    listener.PacketEvent(incoming);

                                _zeroconf.HandlePacket(incoming);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LOGGER.Warn("Failed receiving/sending packet!", ex);
                    }
                }
            }
        }
    }
}