#some modifications from https://github.com/BlueAmulet/ESRGAN
import sys
import os.path
import glob
import cv2
import numpy as np
import torch
import architecture as arch
import re

model_path = sys.argv[1]
upscaleSize = int(sys.argv[2])
deviceName = sys.argv[3]
device = torch.device(deviceName)

test_img_folder = sys.argv[4]
output_folder = sys.argv[5]

mode = sys.argv[6]

passAsString = sys.argv[7]

state_dict = torch.load(model_path)

if 'conv_first.weight' in state_dict:
            print('Attempting to convert and load a new-format model')
            old_net = {}
            items = []
            for k, v in state_dict.items():
                items.append(k)

            old_net['model.0.weight'] = state_dict['conv_first.weight']
            old_net['model.0.bias'] = state_dict['conv_first.bias']

            for k in items.copy():
                if 'RDB' in k:
                    ori_k = k.replace('RRDB_trunk.', 'model.1.sub.')
                    if '.weight' in k:
                        ori_k = ori_k.replace('.weight', '.0.weight')
                    elif '.bias' in k:
                        ori_k = ori_k.replace('.bias', '.0.bias')
                    old_net[ori_k] = state_dict[k]
                    items.remove(k)

            old_net['model.1.sub.23.weight'] = state_dict['trunk_conv.weight']
            old_net['model.1.sub.23.bias'] = state_dict['trunk_conv.bias']
            old_net['model.3.weight'] = state_dict['upconv1.weight']
            old_net['model.3.bias'] = state_dict['upconv1.bias']
            old_net['model.6.weight'] = state_dict['upconv2.weight']
            old_net['model.6.bias'] = state_dict['upconv2.bias']
            old_net['model.8.weight'] = state_dict['HRconv.weight']
            old_net['model.8.bias'] = state_dict['HRconv.bias']
            old_net['model.10.weight'] = state_dict['conv_last.weight']
            old_net['model.10.bias'] = state_dict['conv_last.bias']
            state_dict = old_net

# extract model information
scale2 = 0
max_part = 0
for part in list(state_dict):
    parts = part.split(".")
    n_parts = len(parts)
    if n_parts == 5 and parts[2] == 'sub':
        nb = int(parts[3])
    elif n_parts == 3:
        part_num = int(parts[1])
        if part_num > 6 and parts[2] == 'weight':
            scale2 += 1
        if part_num > max_part:
            max_part = part_num
            out_nc = state_dict[part].shape[0]
upscaleSize = 2 ** scale2
in_nc = state_dict['model.0.weight'].shape[1]
nf = state_dict['model.0.weight'].shape[0]

model = arch.RRDB_Net(in_nc, out_nc, nf, nb, gc=32, upscale=upscaleSize, norm_type=None, act_type='leakyrelu', \
                        mode='CNA', res_scale=1, upsample_mode='upconv')
model.load_state_dict(state_dict, strict=True)
del state_dict
model.eval()
for k, v in model.named_parameters():
    v.requires_grad = False
model = model.to(device)

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

        if img.shape[2] == 3:
            img = img[:, :, [2, 1, 0]]
        elif img.shape[2] == 4:
            img = img[:, :, [2, 1, 0, 3]]
        img = torch.from_numpy(np.transpose(img, (2, 0, 1))).float()
        img_LR = img.unsqueeze(0)
        img_LR = img_LR.to(device)
        output = model(img_LR).data.squeeze(0).float().cpu().clamp_(0, 1).numpy()
        if output.shape[0] == 3:
            output = output[[2, 1, 0], :, :]
        elif output.shape[0] == 4:
            output = output[[2, 1, 0, 3], :, :]
        output = np.transpose(output, (1, 2, 0))
        output = (output * 255.0).round()
        if passAsString == True:            
            buffer = cv2.imencode(".png", output)[1]
            data = base64.b64encode(buffer)
            print(data)
            continue

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
    
