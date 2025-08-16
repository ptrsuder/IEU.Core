#some modifications from https://github.com/BlueAmulet/ESRGAN
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

model_path = sys.argv[1]
deviceName = sys.argv[2]
device = torch.device(deviceName)

test_img_folder = sys.argv[3]
output_folder = sys.argv[4]

mode = sys.argv[5]

passAsString = sys.argv[6]

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
   
in_nc = model_descriptor.input_channels
out_nc = model_descriptor.output_channels
scale = model_descriptor.scale

print('Model: {:s}.\n'.format(os.path.basename(model_path)))
sys.stdout.flush()
alphanum = lambda item: (int(re.findall('\d+', item)[0]) if item[0].isdigit() else float('inf'), item)
idx = 0
test_img_folder = test_img_folder.replace('*','')

for path, subdirs, files in sorted(os.walk(test_img_folder), key=alphanum):
    for name in sorted(files, key=alphanum):
        idx += 1
        base = os.path.splitext(os.path.basename(name))[0]
        inputpath = os.path.join(path, name)
        outputpath = os.path.join(path, name).replace(test_img_folder,'')       
        # read image
        img = cv2.imdecode(np.fromfile(inputpath, dtype=np.uint8), cv2.IMREAD_UNCHANGED)  
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

        # if img.shape[2] == 3:
        #     img = img[:, :, [2, 1, 0]]
        # elif img.shape[2] == 4:
        #     img = img[:, :, [2, 1, 0, 3]]
        # if should_use_fp16:
        #     img = torch.from_numpy(np.transpose(img, (2, 0, 1))).half()
        # else:
        #     img = torch.from_numpy(np.transpose(img, (2, 0, 1))).float() 
                
        dtype = torch.float16 if use_fp16 else torch.float32
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
            
        # img_LR = img.unsqueeze(0)
        # img_LR = img_LR.to(device)
            
        img_LR = t;         
           
        output0 = model_descriptor.model(img_LR)

        output = output0.data.squeeze(0).float().cpu().clamp_(0, 1).numpy()
        if output.shape[0] == 3:
                output = output[[2, 1, 0], :, :]
        elif output.shape[0] == 4:
            output = output[[2, 1, 0, 3], :, :]

        output = np.transpose(output, (1, 2, 0))
        output = (output * 255.0).round()

        newpath = base
        printpath = ''
        if mode == '1' or mode == '2':
            baseinput = os.path.splitext(os.path.basename(name))[0]
            baseinput = re.search('(.*)(_tile-[0-9]+)', baseinput, re.IGNORECASE).group(1)
            modelname = os.path.splitext(os.path.basename(model_path))[0]
        if mode == '1':
            os.makedirs('{1:s}/Images/{0:s}/'.format(baseinput, output_folder), exist_ok=True)
            newpath = '{3:s}/Images/{0:s}/[{2:s}]_{1:s}.png'.format(baseinput, base, modelname, output_folder)
            printpath = base
        if mode == '2':
            os.makedirs('{1:s}/Models/{0:s}/'.format(modelname, output_folder), exist_ok=True)
            newpath = '{2:s}/Models/{0:s}/{1:s}.png'.format(modelname, base, output_folder)
            printpath = base 
        if mode == '0' or mode == '3':
            newpath = path.replace(test_img_folder,'');
            os.makedirs('{1:s}/{0:s}/'.format(newpath, output_folder), exist_ok=True)
            newpath = '{1:s}/{0:s}'.format(outputpath, output_folder)
        printpath = outputpath    
                    
        cv2.imencode(newpath, output)[1].tofile(newpath)     
        print(idx, printpath)
        sys.stdout.flush()

    if mode==0:
        break
    
