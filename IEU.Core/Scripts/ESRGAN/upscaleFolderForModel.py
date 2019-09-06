#modifications from https://github.com/BlueAmulet/ESRGAN/blob/master/test.py
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

state_dict = torch.load(model_path)

if 'conv_first.weight' in state_dict:
    raise ValueError("Attempted to load a new architecture model")

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
upscale = 2 ** scale2
in_nc = state_dict['model.0.weight'].shape[1]
nf = state_dict['model.0.weight'].shape[0]

model = arch.RRDB_Net(in_nc, out_nc, nf, nb, gc=32, upscale=upscaleSize, norm_type=None, act_type='leakyrelu', \
                        mode='CNA', res_scale=1, upsample_mode='upconv')
model.load_state_dict(state_dict, strict=True)
model.eval()
for k, v in model.named_parameters():
    v.requires_grad = False
model = model.to(device)

print('Model path {:s}. \nProcessing...'.format(model_path))
sys.stdout.flush()

idx = 0
files = glob.iglob(test_img_folder)
files_i_care_about = filter(lambda x: not os.path.isdir(x), files)
for path in files_i_care_about:
    idx += 1
    base = os.path.splitext(os.path.basename(path))[0]
    # read image
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    img = img * 1. / np.iinfo(img.dtype).max


    if img.ndim == 2:
        img = np.tile(np.expand_dims(img, axis=2), (1, 1, min(in_nc, 3)))
    if img.shape[2] > in_nc: # remove extra channels
        print("Warning: Truncating image channels")
        sys.stdout.flush()
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

    output = model(img_LR).data.squeeze().float().cpu().clamp_(0, 1).numpy()
    if output.shape[0] == 3:
        output = output[[2, 1, 0], :, :]
    elif output.shape[0] == 4:
        output = output[[2, 1, 0, 3], :, :]
    output = np.transpose(output, (1, 2, 0))
    output = (output * 255.0).round()
    modelname = os.path.splitext(os.path.basename(model_path))[0]    
    if not os.path.exists('{1:s}/Models/{0:s}/'.format(modelname, output_folder)):
        os.makedirs('{1:s}/Models/{0:s}/'.format(modelname, output_folder))
    cv2.imwrite('{2:s}/Models/{0:s}/{1:s}.png'.format(modelname, base, output_folder), output)
    print(idx, base)
    sys.stdout.flush()
