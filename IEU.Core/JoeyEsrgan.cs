using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace ImageEnhancingUtility.Core
{
    public class JoeyEsrgan : ReactiveObject
    {
        string _argumentString;
        [Browsable(false)]
        public string ArgumentString
        {            
            get
            {
                return $"\"{Model}\" --input \"{Input}\" --output \"{Output}\" {(Reverse ? "--reverse" : "")} {(SkipExisting ? "--skip_existing" : "")} --tile_size {TileSize}" +
                    $" {(Seamless ? "--seamless" : "")} {(Mirror ? "--mirror" : "")} {(CPU?"--cpu":"")}" +
                    $" {(BinaryAlpha ? "--binary_alpha" : "")} --alpha_threshold {AlphaThreshold.ToString().Replace(",", ".")} --alpha_boundary_offset {AlphaBoundaryOffset.ToString().Replace(",", ".")} --alpha_mode {AlphaMode}";
            }
        }

        public string Model;

        public string Input = "input";
        public string Output = "output";

        public bool Reverse { get; set; } = false;
        public bool SkipExisting { get; set; } = false;

        public int TileSize = 512;

        public bool Seamless = false;
        public bool Mirror { get; set; } = false;

        public bool CPU = false;

        public bool BinaryAlpha { get; set; } = false;
        public double AlphaThreshold { get; set; } = 0.5;
        public double AlphaBoundaryOffset { get; set; } = 0.2;
        public int AlphaMode { get; set; } = 1;


        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changed { get; }
        [Browsable(false)]
        public new IObservable<IReactivePropertyChangedEventArgs<IReactiveObject>> Changing { get; }
        [Browsable(false)]
        public new IObservable<Exception> ThrownExceptions { get; }
    }
}
