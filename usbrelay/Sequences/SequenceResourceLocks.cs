using System.Collections.Generic;

namespace usbrelay.Sequences
{
    public sealed class SequenceResourceLocks
    {
        private readonly object syncRoot = new object();
        private readonly Dictionary<RelayResource, string> owners = new Dictionary<RelayResource, string>();

        public bool TryReserve(string owner, IEnumerable<RelayResource> resources)
        {
            lock (syncRoot)
            {
                var requested = new List<RelayResource>(resources);
                foreach (var resource in requested)
                {
                    if (owners.ContainsKey(resource))
                        return false;
                }

                foreach (var resource in requested)
                    owners[resource] = owner;

                return true;
            }
        }

        public bool IsBusy(RelayResource resource)
        {
            lock (syncRoot)
                return owners.ContainsKey(resource);
        }

        public void Release(string owner)
        {
            lock (syncRoot)
            {
                var toRelease = new List<RelayResource>();
                foreach (var pair in owners)
                {
                    if (pair.Value == owner)
                        toRelease.Add(pair.Key);
                }

                foreach (var resource in toRelease)
                    owners.Remove(resource);
            }
        }
    }
}
