using System.Collections.Generic;
using ProtoBuf;
using ReactiveUI;

namespace ImageEnhancingUtility.Core
{
    [ProtoContract]
    public class Rule : ReactiveObject
    {
        [ProtoMember(1)]
        public string Name { get; set; }
        [ProtoMember(2)]
        public Profile Profile { get; set; }
        [ProtoMember(3)]
        public Filter Filter { get; set; }
        [ProtoMember(4)]
        public int Priority = 0;

        public Rule() { }
        public Rule(string name, Profile profile, Filter filter)
        {
            Name = name;
            Profile = profile;
            Filter = filter;
        }
    }

    public class RulePriority : Comparer<int>
    {
        public override int Compare(int x, int y)
        {
            if (x > y)
                return y.CompareTo(x);
            else
                return x.CompareTo(y);
        }

    }
}
