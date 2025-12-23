using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using lib.crypto;

namespace zeroconf.zeroconf
{
    /// <summary>
    /// Service represents a Service to be announced by the Zeroconf class.
    /// </summary>
    public class Service
    {
        private String _alias;
        private String _service;
        private int _port;
        private Dictionary<String, String> _text;
        private List<IPAddress> _addresses = new List<IPAddress>();
        private String _domain;
        private String _protocol;
        private String _host;
        
        /// <summary>
        /// Create a new {@link Service} to be announced by this object.
        /// <br />
        /// A JmDNS `type` field of "_foobar._tcp.local." would be specified here as a `service` param of "foobar".
        /// </summary>
        /// <param name="alias">the servce alias eg "My Web Server"</param>
        /// <param name="service">the service type, eg "http"</param>
        /// <param name="port">the service port</param>
        /// <exception cref="InvalidOperationException"></exception>
        public Service(String alias, String service, int port)
        {
            _alias = alias;
            for (int i = 0; i < _alias.Length; i++)
            {
                char c = _alias.ToCharArray()[i];
                if (c < 0x20 || c == 0x7F)
                    throw new InvalidOperationException();
            }
            
            _service = service;
            _port = port;
            _protocol = "tcp";
            _text = new Dictionary<String, String>();
        }

        private static void Esc(String input, StringBuilder builder)
        {
            for (int i = 0; i < input.Length; i++)
            {
                char c = input.ToCharArray()[i];
                if (c == '.' || c == '\\') builder.Append('\\');
                builder.Append(c);
            }
        }
        
        public override String ToString() {
            return "Service(" +
                   "alias='" + _alias + '\'' +
                   ", service='" + _service + '\'' +
                   ", port=" + _port +
                   ", text=" + _text +
                   ", addresses=" + _addresses +
                   ", domain='" + _domain + '\'' +
                   ", protocol='" + _protocol + '\'' +
                   ", host='" + _host + '\'' +
                   ')';
        }
        
        /// <summary>
        /// Set the protocol, which can be one of "tcp" (the default) or "udp"
        /// </summary>
        /// <param name="protocol">the protocol</param>
        /// <returns>this</returns>
        /// <exception cref="InvalidOperationException">if an invalid protocol was specified</exception>
        public Service SetProtocol(String protocol)
        {
            if ("tcp".Equals(protocol) || "udp".Equals(protocol)) _protocol = protocol;
            else throw new InvalidOperationException();
            return this;
        }
        
        /// <returns>the domain</returns>
        public String GetDomain()
        {
            return _domain;
        }

        /// <summary>
        /// Set the domain, which defaults to <see cref="Zeroconf.GetDomain"/> and must begin with "."
        /// </summary>
        /// <param name="domain">the domain</param>
        /// <returns>this</returns>
        /// <exception cref="InvalidOperationException">if an invalid domain was specified</exception>
        public Service SetDomain(String domain)
        {
            if (domain == null || domain.Length < 2 || domain.ToCharArray()[0] != '.')
                throw new InvalidOperationException();
            
            _domain = domain;
            return this;
        }

        /// <returns>the host</returns>
        public String GetHost()
        {
            return _host;
        }

        /// <summary>
        /// Set the host which is hosting this service, which defaults to <see cref="Zeroconf.GetLocalHostName" />.
        /// It is possible to announce a service on a non-local host
        /// </summary>
        /// <param name="host">the host</param>
        /// <returns>this</returns>
        public Service SetHost(String host)
        {
            _host = host;
            return this;
        }

        /// <summary>
        /// Set the Text record to go with this Service, which is of the form "key1=value1, key2=value2"
        /// Any existing Text records are replaced
        /// </summary>
        /// <param name="text">the text</param>
        /// <returns>this</returns>
        /// <exception cref="InvalidOperationException">if an invalid entry was found in text</exception>
        public Service SetText(String text)
        {
            _text.Clear();
            String[] q = Regex.Split(text, ", *");
            foreach (string s in q)
            {
                String[] r = s.Split('=');
                if (r.Length == 2) _text[r[0]] = r[1];
                else throw new InvalidOperationException();
            }

            return this;
        }

        /// <summary>
        /// Set the Text record to go with this Service, which is specified as a Map of keys and values
        /// Any existing Text records are replaced
        /// </summary>
        /// <param name="text">the text</param>
        /// <returns>this</returns>
        public Service SetText(Dictionary<String, String> text)
        {
            _text.Clear();
            foreach (KeyValuePair<String, String> pair in text)
                _text.Add(pair.Key, pair.Value);
            return this;
        }

        /// <summary>
        /// Add a Text record entry to go with this Service to the existing list of Text record entries.
        /// </summary>
        /// <param name="key">the text key</param>
        /// <param name="value">the corresponding value</param>
        /// <returns>this</returns>
        public Service PutText(String key, String value)
        {
            _text.Add(key, value);
            return this;
        }

        /// <summary>
        /// Add an InetAddress to the list of addresses for this service. By default they are taken
        /// from <see cref="Zeroconf.GetLocalAddresses" />, as the hostname is taken from <see cref="Zeroconf.GetLocalHostName" />.
        /// If advertising a Service on a non-local host, the addresses must be set manually using this
        /// method.
        /// </summary>
        /// <param name="address">the InetAddress this Service resides on</param>
        /// <returns>this</returns>
        public Service AddAddress(IPAddress address)
        {
            _addresses.Add(address);
            return this;
        }

        public Service AddAddresses(List<IPAddress> addresses)
        {
            _addresses.AddRange(addresses);
            return this;
        }
        
        /// <returns>whether the service has addresses to announce</returns>
        public bool HasAddresses()
        {
            return _addresses.Count != 0;
        }
        
        /// <returns>the alias</returns>
        public String GetAliaas()
        {
            return _alias;
        }

        /// <summary>
        /// Return the instance-name for this service. This is the "fully qualified domain name" of
        /// the service and looks something like "My Service._http._tcp.local"
        /// </summary>
        /// <returns>the instance name</returns>
        public String GetInstanceName()
        {
            StringBuilder sb = new StringBuilder();
            Esc(_alias, sb);
            sb.Append("._");
            Esc(_service, sb);
            sb.Append("._");
            sb.Append(_protocol);
            sb.Append(_domain);
            return sb.ToString();
        }

        /// <summary>
        /// Return the service-name for this service. This is the "domain name" of
        /// the service and looks something like "._http._tcp.local" - i.e. the InstanceName
        /// without the alias. Note the rather ambiguous term "service name" comes from the spec.
        /// </summary>
        /// <returns>the service name</returns>
        public String GetServiceName()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("._");
            Esc(_service, sb);
            sb.Append("._");
            sb.Append(_protocol);
            sb.Append(_domain);
            return sb.ToString();
        }

        internal Packet GetPacket()
        {
            Packet packet = new Packet();
            packet.SetAuthoritative(true);

            String fqdn = GetInstanceName();
            String ptrname = GetServiceName();
            
            packet.AddAnswer(new RecordPTR(ptrname, fqdn).SetTTL(28800));
            packet.AddAnswer(new RecordSRV(fqdn, _host, _port).SetTTL(120));
            if (_text.Count != 0) packet.AddAnswer(new RecordTXT(fqdn, _text).SetTTL(120));

            foreach (IPAddress address in _addresses)
                packet.AddAnswer(new RecordAORAAAA(_host, address));

            return packet;
        }
    }
}