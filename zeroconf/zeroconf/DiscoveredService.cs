using System;
using System.Collections.Generic;
using lib.core;

namespace zeroconf.zeroconf
{
    public class DiscoveredService
    {
        public String Target;
        public int Port;
        public String Name;
        public String Service;
        public String Protocol;
        public String Domain;
        public String ServiceName;
        private long Expiration;
        private List<Record> _relatedRecords = new List<Record>();

        internal DiscoveredService(RecordSRV record)
        {
            Expiration = TimeProvider.currentTimeMillis() + record.TTL * 1000L;

            Target = record.GetTarget();
            Port = record.GetPort();
            ServiceName = record.GetName();

            String[] split = ServiceName.Split('.');
            if (split.Length != 4) throw new InvalidOperationException("Invalid service name: " + record.GetName());
            
            Name = split[0];
            Service = split[1];
            Protocol = split[2];
            Domain = "." + split[3];
        }

        public bool IsExpired()
        {
            return TimeProvider.currentTimeMillis() > Expiration;
        }

        internal void AddRelatedRecord(Record record)
        {
            _relatedRecords.RemoveAll(r => r.IsExpired());
            _relatedRecords.Add(record);
        }

        public List<Record> GetRelatedRecords()
        {
            return new List<Record>(_relatedRecords);
        }

        public bool Equals(Object o)
        {
            if (this == o) return true;
            if (o == null || typeof(DiscoveredService).IsAssignableFrom(o.GetType())) return false;
            DiscoveredService that = (DiscoveredService)o;
            if (Port != that.Port) return false;
            if (!Target.Equals(that.Target)) return false;
            if (!Name.Equals(that.Name)) return false;
            if (!Service.Equals(that.Service)) return false;
            if (!Protocol.Equals(that.Protocol)) return false;
            return Domain.Equals(that.Domain);
        }

        public override int GetHashCode()
        {
            int result = Target.GetHashCode();
            result = 31 * result + Port;
            result = 31 * result + Name.GetHashCode();
            result = 31 * result + Service.GetHashCode();
            result = 31 * result + Protocol.GetHashCode();
            result = 31 * result + Domain.GetHashCode();
            return result;
        }

        public override string ToString()
        {
            return "DiscoveredService(" +
                   "target='" + Target + '\'' +
                   ", port=" + Port +
                   ", name='" + Name + '\'' +
                   ", service='" + Service + '\'' +
                   ", protocol='" + Protocol + '\'' +
                   ", domain='" + Domain + '\'' +
                   ')';
        }
    }
}