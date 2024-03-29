#some modifications from https://github.com/BlueAmulet/ESRGAN
import sys
import os.path
import glob
import cv2
import numpy as np
import torch
import architecture as arch
import re
import io
import base64
import json
from urllib.parse import unquote

blank = sys.argv[1]
upscaleSize = int(sys.argv[2])
deviceName = sys.argv[3]
device = torch.device(deviceName)

test_img_folder = sys.argv[4]
output_folder = sys.argv[5]
outMode = sys.argv[6]
passAsString = sys.argv[7]

line = sys.stdin.readline()
model_paths = json.loads(unquote(line))

for model_path in model_paths:
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
            data = lrImages[path]
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
            print('b\'{0}=:::{1}:::{2}:::{3}'.format(data, outpath, path, modelname))
            sys.stdout.flush()