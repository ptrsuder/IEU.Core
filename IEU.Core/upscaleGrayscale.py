import sys
import os.path
import glob
import cv2
import numpy as np
import torch
import architecture as arch



model_path = sys.argv[1]  # models/RRDB_ESRGAN_x4.pth OR models/RRDB_PSNR_x4.pth
upscaleSize = int(sys.argv[2])
deviceName = sys.argv[3]
device = torch.device(deviceName)  # if you want to run on CPU, change 'cuda' -> cpu

test_img_folder = sys.argv[4]
output_folder = sys.argv[5]

model = arch.RRDB_Net(1, 1, 64, 23, gc=32, upscale=upscaleSize, norm_type=None, act_type='leakyrelu', \
                        mode='CNA', res_scale=1, upsample_mode='upconv')
model.load_state_dict(torch.load(model_path), strict=True)
model.eval()
for k, v in model.named_parameters():
    v.requires_grad = False
model = model.to(device)

print('Model path {:s}. \nTesting...'.format(model_path))
sys.stdout.flush()

idx = 0
files = glob.iglob(test_img_folder)
files_i_care_about = filter(lambda x: not os.path.isdir(x), files)
for path in files_i_care_about:
    idx += 1
    base = os.path.splitext(os.path.basename(path))[0]    
    # read image
    img = cv2.imread(path, cv2.IMREAD_GRAYSCALE)
    img = img * 1.0 / 255
    img = torch.from_numpy(np.transpose(img[:, :, None], (2, 0, 1))).float()
    img_LR = img.unsqueeze(0)
    img_LR = img_LR.to(device)

    output = model(img_LR).data.squeeze().float().cpu().clamp_(0, 1).numpy()
    output = np.transpose(output[None, :, :], (1, 2, 0))
    output = (output * 255.0).round()
    cv2.imwrite('{1:s}\{0:s}.png'.format(base, output_folder), output)
    print(idx, base)
    sys.stdout.flush()