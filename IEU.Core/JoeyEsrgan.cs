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
    public enum SeamlessMode
    {
        None,
        Tile,
        Mirror,
        Replicate,
        AlphaPad
    }

    public enum AlphaMode
    {
        no_alpha,
        bg_difference,
        alpha_separately,
        swapping
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
                return $"\"{ModelsArgument}\" --input \"{Input}\" --output \"{Output}\" {(fp16 ? "--fp16" : "")} {(Reverse ? "--reverse" : "")} {(SkipExisting ? "--skip-existing" : "")} {(DeleteInput ? "--delete-input" : "")} {(VerboseMode ? "--verbose" : "")}" +
                    $" {seamlessModeArgument} {(Mirror ? "--mirror" : "")} {(CPU?"--cpu":"")} {(CacheMaxSplitDepth? "--cache-max-split-depth":"")}"+
                    $" {(BinaryAlpha ? "--binary-alpha" : "")} {(TernaryAlpha ? "--ternary-alpha" : "")} --alpha-threshold {AlphaThreshold.ToString().Replace(",", ".")} --alpha-boundary-offset {AlphaBoundaryOffset.ToString().Replace(",", ".")} --alpha-mode {alphaModeArgument}";
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
        public SeamlessMode SeamlessMode { get; set; } = SeamlessMode.None;

        Dictionary<SeamlessMode, string> seamlessModeArguments = new Dictionary<SeamlessMode, string>
        { 
            { SeamlessMode.None, "" },
            { SeamlessMode.Tile, "--seamless tile" },
            { SeamlessMode.Mirror, "--seamless mirror"  },
            { SeamlessMode.Replicate, "--seamless replicate" },
            { SeamlessMode.AlphaPad, "--seamless alpha_pad" }
        };
       
        string seamlessModeArgument { get => seamlessModeArguments[SeamlessMode]; }

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

        public AlphaMode AlphaMod { get; set; } = AlphaMode.no_alpha;
        Dictionary<AlphaMode, string> alphaModArguments = new Dictionary<AlphaMode, string>
        {
            { AlphaMode.no_alpha, "none" },
            { AlphaMode.bg_difference, "bg_difference" },
            { AlphaMode.alpha_separately, "separate"  },
            { AlphaMode.swapping, "swapping" }          
        };
        string alphaModeArgument { get => alphaModArguments[AlphaMod]; }

        [ProtoMember(10)]
        public bool CacheMaxSplitDepth { get; set; } = false;
        [ProtoMember(11)]
        public bool fp16 { get; set; } = false;

        [ProtoMember(12)]
        public bool TernaryAlpha { get; set; } = false;

        [ProtoMember(13)]
        public bool DeleteInput { get; set; } = false;
        [ProtoMember(14)]
        public bool VerboseMode { get; set; } = false;
        


        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changed { get; }
        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changing { get; }
        [Browsable(false)]
        public new IObservable<Exception> ThrownExceptions { get; }
    }
}
