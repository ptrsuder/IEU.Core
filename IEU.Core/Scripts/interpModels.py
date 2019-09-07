import sys
import torch
from collections import OrderedDict

alpha = float(sys.argv[3])
net_PSNR_path = sys.argv[1]
net_ESRGAN_path = sys.argv[2]
net_interp_path = sys.argv[4]

net_PSNR = torch.load(net_PSNR_path)
net_ESRGAN = torch.load(net_ESRGAN_path)
net_interp = OrderedDict()

print('Interpolating with alpha = ', alpha)

for k, v_PSNR in net_PSNR.items():
    v_ESRGAN = net_ESRGAN[k]
    net_interp[k] = (1 - alpha) * v_PSNR + alpha * v_ESRGAN

torch.save(net_interp, net_interp_path)
