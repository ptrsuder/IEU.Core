using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using ReactiveUI;

namespace ImageEnhancingUtility.Core
{    
    public enum SeamlessMod
    {
        None,
        Tile,
        Mirror,
        Replicate,
        AlphaPad
    }

    [ProtoContract]
    public class JoeyEsrgan : ReactiveObject
    {
        public JoeyEsrgan() {}

        [Browsable(false)]
        public string ArgumentString
        {            
            get
            {
                return $"\"{ModelsArgument}\" --input \"{Input}\" --output \"{Output}\" {(Reverse ? "--reverse" : "")} {(SkipExisting ? "--skip_existing" : "")}" +
                    $" {seamlessModArgument} {(Mirror ? "--mirror" : "")} {(CPU?"--cpu":"")} {(CacheMaxSplitDepth? "--cache_max_split_depth":"")}"+
                    $" {(BinaryAlpha ? "--binary_alpha" : "")} --alpha_threshold {AlphaThreshold.ToString().Replace(",", ".")} --alpha_boundary_offset {AlphaBoundaryOffset.ToString().Replace(",", ".")} --alpha_mode {AlphaMode}";
            }
        }

        public string ModelsArgument;

        public string Input = "input";
        public string Output = "output";

        [ProtoMember(1)]
        public bool Reverse { get; set; } = false;
        [ProtoMember(2)]
        public bool SkipExisting { get; set; } = false;

        [ProtoMember(3)]
        public SeamlessMod SeamlessMod { get; set; } = SeamlessMod.None;

        Dictionary<SeamlessMod, string> seamlessModArguments = new Dictionary<SeamlessMod, string>
        { 
            { SeamlessMod.None, "" },
            { SeamlessMod.Tile, "--seamless tile" },
            { SeamlessMod.Mirror, "--seamless mirror"  },
            { SeamlessMod.Replicate, "--seamless replicate" },
            { SeamlessMod.AlphaPad, "--seamless alpha_pad" }
        };
              
        string seamlessModArgument { get => seamlessModArguments[SeamlessMod]; }

        [ProtoMember(4)]
        public bool Mirror { get; set; } = false;
        [ProtoMember(5)]
        public bool CPU { get; set; } = false;

        [ProtoMember(6)]
        public bool BinaryAlpha { get; set; } = false;
        [ProtoMember(7)]
        public double AlphaThreshold { get; set; } = 0.5;
        [ProtoMember(8)]
        public double AlphaBoundaryOffset { get; set; } = 0.2;
        [ProtoMember(9)]
        public int AlphaMode { get; set; } = 1;
        [ProtoMember(10)]
        public bool CacheMaxSplitDepth { get; set; } = false;

        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changed { get; }
        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changing { get; }
        [Browsable(false)]
        public new IObservable<Exception> ThrownExceptions { get; }
    }
}
