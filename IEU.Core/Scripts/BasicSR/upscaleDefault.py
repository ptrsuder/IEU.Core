import os
import os.path
import sys
import logging
import time
import argparse
import numpy as np
from collections import OrderedDict

import options.options as option
import utils.util as util
from data.util import bgr2ycbcr
from data import create_dataset, create_dataloader
from models import create_model

def main():
    # options
    parser = argparse.ArgumentParser()
    parser.add_argument('-opt', type=str, required=True, help='Path to options JSON file.')
    opt = option.parse(parser.parse_args().opt, is_train=False)
    #util.mkdirs((path for key, path in opt['path'].items() if not key == 'pretrain_model_G'))
    opt = option.dict_to_nonedict(opt)

    #util.setup_logger(None, opt['path']['log'], 'test.log', level=logging.INFO, screen=True)
    #logger = logging.getLogger('base')
    #logger.info(option.dict2str(opt))
    # Create test dataset and dataloader
    test_loaders = []
    for phase, dataset_opt in sorted(opt['datasets'].items()):
        test_set = create_dataset(dataset_opt)
        test_loader = create_dataloader(test_set, dataset_opt)
        #logger.info('Number of test images in [{:s}]: {:d}'.format(dataset_opt['name'], len(test_set)))
        test_loaders.append(test_loader)

    # Create model
    model = create_model(opt)

    modelKey = 'SR'
    if opt['model'] == 'ppon':
        modelKey = 'img_p'
        print('Model is recognized as PPON model')

    for test_loader in test_loaders:
        test_set_name = test_loader.dataset.opt['name']
        print('\nTesting [{:s}]...'.format(test_set_name))
        #logger.info('\nTesting [{:s}]...'.format(test_set_name))
        test_start_time = time.time()
        #dataset_dir = os.path.join(opt['path']['results_root'], test_set_name)
        dataset_dir = test_loader.dataset.opt['dataroot_HR']
        #util.mkdir(dataset_dir)

        idx = 0
        for data in test_loader:
            idx += 1
            need_HR = False #if test_loader.dataset.opt['dataroot_HR'] is None else True

            model.feed_data(data, need_HR=need_HR)
            img_path = data['LR_path'][0]
            img_name = os.path.splitext(os.path.basename(img_path))[0]

            model.test()  # test
            visuals = model.get_current_visuals(need_HR=need_HR)

            sr_img = util.tensor2img(visuals[modelKey])  # uint8

            # save images
            baseinput = os.path.splitext(os.path.basename(img_path))[0][:-8]
            model_path = opt['path']['pretrain_model_G']
            modelname = os.path.splitext(os.path.basename(model_path))[0]
            save_img_path = os.path.join(dataset_dir, img_name + '.png')
            util.save_img(sr_img, save_img_path)
            print(idx, img_name + '.png')
            sys.stdout.flush()

if __name__ == '__main__':
    main()
