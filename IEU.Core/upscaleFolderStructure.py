import sys
import os.path
import glob
import cv2
import numpy as np
import torch
import architecture as arch



model_path = sys.argv[1] 
upscaleSize = int(sys.argv[2])
deviceName = sys.argv[3]
device = torch.device(deviceName)

model = arch.RRDB_Net(3, 3, 64, 23, gc=32, upscale=upscaleSize, norm_type=None, act_type='leakyrelu', \
                        mode='CNA', res_scale=1, upsample_mode='upconv')
model.load_state_dict(torch.load(model_path), strict=True)
model.eval()
for k, v in model.named_parameters():
    v.requires_grad = False
model = model.to(device)

print('Model path {:s}. \nTesting...'.format(model_path))
sys.stdout.flush()

idx = 0

for path, subdirs, files in os.walk('LR'):
    for name  in files:
        idx += 1        
        inputpath = os.path.join(path, name)
        print(inputpath)
        outputpath = os.path.join(path, name).replace('LR','')
        # read image
        img = cv2.imread(inputpath, cv2.IMREAD_COLOR)
        img = img * 1.0 / 255
        img = torch.from_numpy(np.transpose(img[:, :, [2, 1, 0]], (2, 0, 1))).float()
        img_LR = img.unsqueeze(0)
        img_LR = img_LR.to(device)
        output = model(img_LR).data.squeeze().float().cpu().clamp_(0, 1).numpy()
        output = np.transpose(output[[2, 1, 0], :, :], (1, 2, 0))
        output = (output * 255.0).round()        
        if not os.path.exists('results/{:s}'.format(path.replace('LR',''))):
            os.makedirs('results/{:s}/'.format(path.replace('LR','')))
        cv2.imwrite('results/{:s}'.format(outputpath), output)
        print(idx, outputpath)
        sys.stdout.flush()