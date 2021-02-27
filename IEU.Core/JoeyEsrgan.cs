using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    
    public class JoeyEsrgan : ReactiveObject
    {
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

        public bool Reverse { get; set; } = false;
        public bool SkipExisting { get; set; } = false;

        public SeamlessMod SeamlessMod { get; set; } = SeamlessMod.Tile;

        Dictionary<SeamlessMod, string> seamlessModArguments = new Dictionary<SeamlessMod, string>
        { 
            { SeamlessMod.None, "" },
            { SeamlessMod.Tile, "--seamless tile" },
            { SeamlessMod.Mirror, "--seamless mirror"  },
            { SeamlessMod.Replicate, "--seamless replicate" },
            { SeamlessMod.AlphaPad, "--seamless alpha_pad" }
        };

        string seamlessModArgument { get => seamlessModArguments[SeamlessMod]; }

        public bool Mirror { get; set; } = false;

        public bool CPU { get; set; } = false;

        public bool BinaryAlpha { get; set; } = false;
        public double AlphaThreshold { get; set; } = 0.5;
        public double AlphaBoundaryOffset { get; set; } = 0.2;
        public int AlphaMode { get; set; } = 1;
        public bool CacheMaxSplitDepth { get; set; } = false;

        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changed { get; }
        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changing { get; }
        [Browsable(false)]
        public new IObservable<Exception> ThrownExceptions { get; }
    }
}
