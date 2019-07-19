##https://github.com/victorca25/pth-reader/blob/master/pth_param_read.py
import torch
import argparse



parser = argparse.ArgumentParser()
parser.add_argument('-pretrained', '-p', type=str, required=False, help='Path to pretrained model.')
args = parser.parse_args()


print(args.pretrained)

if args.pretrained:
    pretrained_net = torch.load(args.pretrained)

layers_pretrain = []

for k, v in pretrained_net.items():
    layers_pretrain.append(k)

if 'model.3.weight' in layers_pretrain:
    if 'model.6.weight' in layers_pretrain:
        if 'model.8.weight' in layers_pretrain:
            if 'model.10.weight' in layers_pretrain:
                print('4')
        elif 'model.9.weight' in layers_pretrain:
            if 'model.11.weight' in layers_pretrain:
                if 'model.13.weight' in layers_pretrain:
                    print('8')
            elif 'model.12.weight' in layers_pretrain:
                if 'model.14.weight' in layers_pretrain:
                     if 'model.16.weight' in layers_pretrain:
                         print('16')
    elif 'model.5.weight' in layers_pretrain:
        if 'model.7.weight' in layers_pretrain:
            print('2')
elif 'model.2.weight' in layers_pretrain:
    print('1')
