#parts of code from https://github.com/chaiNNer-org/chaiNNer
import sys
import os.path
import glob
import cv2
import numpy as np
from spandrel import MAIN_REGISTRY, ModelDescriptor, ModelLoader
import torch
import re
import io
import base64
import json
from urllib.parse import unquote
#from pytorch.model_loading import load_state_dict
#from pytorch.types import PyTorchModel
#from pytorch.unpickler import RestrictedUnpickle
#from pytorch.utils import to_pytorch_execution_options

blank = sys.argv[1]
deviceName = sys.argv[2]
device = torch.device(deviceName)

test_img_folder = sys.argv[3]
output_folder = sys.argv[4]
outMode = sys.argv[5]
passAsString = sys.argv[6]

line = sys.stdin.readline()
model_paths = json.loads(unquote(line))

for model_path in model_paths:        
  
    model_descriptor = ModelLoader(device).load_from_file(model_path)

    for _, v in model_descriptor.model.named_parameters():
        v.requires_grad = False
    model_descriptor.model.eval()
    use_fp16 = True
    model_descriptor = model_descriptor.to(device)
    should_use_fp16 = use_fp16 and model_descriptor.supports_half
    if should_use_fp16:
        model_descriptor.model.half()
    else:
        model_descriptor.model.float() 

    print('Using fp16: {0}.\n'.format(should_use_fp16))
    sys.stdout.flush()
   
    in_nc = model_descriptor.input_channels
    out_nc = model_descriptor.output_channels
    scale = model_descriptor.scale

    print('Model: {:s}.\n'.format(os.path.basename(model_path)))
    sys.stdout.flush()

    idx = 0
    test_img_folder = test_img_folder.replace('*','')

    #for line in sys.stdin:
    while True:
        line = sys.stdin.readline()
        if not line:
           break
        #print('NEW LINE IS {:s}'.format(line))
        #sys.stdout.flush()
        if line == 'end':
           break
        try:
            lrImages = json.loads(unquote(line))
        except:
            #print('INVALID LINE: {:s}'.format(line))
            #sys.stdout.flush()
            break
        for path in lrImages:
            idx += 1
            base = os.path.splitext(os.path.basename(path))[0]
            inputpath = path
            outputpath = path.replace(test_img_folder,'')
            
            # read image
            img_original = base64.b64decode(lrImages[path])
            img_np = np.frombuffer(img_original, dtype=np.uint8);
            img = cv2.imdecode(img_np, flags=1)        
            img = img * 1. / np.iinfo(img.dtype).max

            if img.ndim == 2:
                img = np.tile(np.expand_dims(img, axis=2), (1, 1, min(in_nc, 3)))
            if img.shape[2] > in_nc: # remove extra channels
                if in_nc != 3 or img.shape[2] != 4 or img[:, :, 3].min() < 1:
                    print("Warning: Truncating image channels")
                    #sys.stdout.flush()
                img = img[:, :, :in_nc]
            elif img.shape[2] == 3 and in_nc == 4: # pad with solid alpha channel
                img = np.dstack((img, np.full(img.shape[:-1], 1.)))
            
                
            dtype = torch.float16 if should_use_fp16 else torch.float32

            if model_descriptor.dtype != dtype or model_descriptor.device != device:
                model_descriptor = model_descriptor.to(device, dtype)

            img = np.ascontiguousarray(img)
            img = np.copy(img)
            input_tensor = torch.from_numpy(img).to(device, dtype)

            t = input_tensor;
            if len(t.shape) == 3 and t.shape[2] == 3:
             # (H, W, C) RGB -> BGR
                 t = t.flip(2)
            elif len(t.shape) == 3 and t.shape[2] == 4:
            # (H, W, C) RGBA -> BGRA
                 t = torch.cat((t[:, :, 2:3], t[:, :, 1:2], t[:, :, 0:1], t[:, :, 3:4]), 2)
               
            if len(t.shape) == 2:
            # (H, W) -> (1, 1, H, W)
                t = t.unsqueeze(0).unsqueeze(0)
            elif len(t.shape) == 3:
            # (H, W, C) -> (1, C, H, W)
                t = t.permute(2, 0, 1).unsqueeze(0)             
            
            img_LR = t;         
           
            output0 = model_descriptor(img_LR)
            output = output0.data.squeeze(0).float().cpu().clamp_(0, 1).numpy()
            #output = output0.detach().cpu().detach().float().numpy()
            
            if output.shape[0] == 3:
                output = output[[2, 1, 0], :, :]
            elif output.shape[0] == 4:
                output = output[[2, 1, 0, 3], :, :]

            output = np.transpose(output, (1, 2, 0))
            output = (output * 255.0).round()
            
            buffer = cv2.imencode(".png", output)[1]
            data = base64.b64encode(buffer)
            outpath = ''
            modelname = os.path.splitext(os.path.basename(model_path))[0]
            if outMode == '1' or outMode == '2':
                baseinput = os.path.splitext(os.path.basename(path))[0]
                baseinput = re.search('(.*)(_tile-[0-9]+)', baseinput, re.IGNORECASE).group(1)
            if outMode == '1':
                outpath = '{3:s}\Images\{0:s}\[{2:s}]_{1:s}.png'.format(baseinput, base, modelname, output_folder)
            if outMode == '2':
                outpath = '{2:s}\Models\{0:s}\{1:s}.png'.format(modelname, base, output_folder)
            if outMode == '0' or outMode == '3':
                outpath = '{1}\{0}'.format(outputpath, output_folder)
            print('{0}:::{1}:::{2}:::{3}'.format(data, outpath, path, modelname))
            sys.stdout.flush()