#https://gist.github.com/BlueAmulet/82a73028bb90a1e763f5790e86609567
from importlib import util
import os
import platform
import sys

machine64bit = platform.machine().endswith('64')
python64bit = (platform.architecture()[0] == '64bit')
python_location = os.path.dirname(sys.executable)
conda_env = os.path.exists(os.path.join(sys.prefix, 'conda-meta'))
install_cmd = 'conda' if conda_env else 'python -m pip'

def find_on_path(name, extra=None):
    locations = []
    dedup = []
    paths = os.environ.get('PATH').split(';')
    if extra is not None:
        paths += extra
    for path in paths:
        cleanpath = os.path.normpath(path).lower()
        if cleanpath not in dedup:
            dedup.append(cleanpath)
            filename = os.path.join(path, name)
            if os.path.exists(filename):
                locations.append(filename)
    return locations

# Scans DLL and imports
visited_dlls = []
found_dups = False
def check_pe(path, dllpaths):
    import pefile
    global visited_dlls, found_dups
    #print('Scanning "' + path + '" ...')
    pe = pefile.PE(path, fast_load=True)
    machine = pe.FILE_HEADER.Machine
    if machine != 0x8664 and machine != 0x014c:
        print('Error: "' + path + '" is not a valid DLL (unknown architecture ' + hex(machine) + ')')
    elif python64bit and machine == 0x014c:
        print('Warning: "' + path + '" is a 32bit DLL')
    elif not python64bit and machine == 0x8664:
        print('Error: "' + path + '" is a 64bit DLL')
    pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY['IMAGE_DIRECTORY_ENTRY_IMPORT']])
    if hasattr(pe, 'DIRECTORY_ENTRY_IMPORT'):
        for entry in pe.DIRECTORY_ENTRY_IMPORT:
            try:
                dll = entry.dll.decode('ascii')
                if dll.lower() not in visited_dlls:
                    visited_dlls.append(dll.lower())
                    locations = find_on_path(dll, dllpaths)
                    if len(locations) == 0:
                        if not (dll.lower().startswith('api-ms-win-') or dll.lower().startswith('ext-ms-')): # Ignore API Sets
                            print('Error: Could not find "' + dll + '" required by "' + path + '"')
                    elif len(locations) > 1:
                        found_dups = True
                        print('Warning: Found multiple copies of "' + dll + '"')
                        for n, path in enumerate(locations, 1):
                            print(n, path)
                    for dllpath in locations:
                        check_pe(dllpath, dllpaths)
            except UnicodeDecodeError:
                print('Warning: Failed to decode import name ' + str(entry.dll))

# Basic DLL check
def check_pe_lite(path):
    #print('Checking "' + path + '" ...')
    with open(path, 'rb') as f:
        header = f.read(2)
        if header != b'MZ':
            print('Error: "' + path + '" is not a valid DLL (missing MZ header)')
            return
        f.seek(0x3C)
        offset = int.from_bytes(f.read(4), byteorder='little')
        f.seek(offset)
        header = f.read(4)
        if header != b'PE\x00\x00':
            print('Error: "' + path + '" is not a valid DLL (missing PE header)')
            return
        machine = int.from_bytes(f.read(2), byteorder='little')
        if machine != 0x8664 and machine != 0x014c:
            print('Error: "' + path + '" is not a valid DLL (unknown architecture ' + hex(machine) + ')')
            return
        if python64bit and machine == 0x014c:
            print('Warning: "' + path + '" is a 32bit DLL')
        elif not python64bit and machine == 0x8664:
            print('Error: "' + path + '" is a 64bit DLL')

def scan_module(path, use_pefile):
    # TODO: Investigate how python locates a dll from a package
    dllpaths = []
    if use_pefile:
        for root, dirs, files in os.walk(path):
            for file in files:
                if file.lower().endswith(('.dll', '.pyd')):
                    if not root in dllpaths:
                        dllpaths.append(root)
    for root, dirs, files in os.walk(path):
        for file in files:
            if file.lower().endswith(('.dll', '.pyd')):
                filename = os.path.join(root, file)
                if use_pefile:
                    check_pe(filename, dllpaths)
                else:
                    check_pe_lite(filename)

if __name__ == "__main__":
    # Check if python is running under conda
    if conda_env:
        conda_name = os.getenv("CONDA_DEFAULT_ENV")
        if conda_name is not None:
            conda_name = "(" + conda_name + ")"
        else:
            conda_name = "unknown environment"
        print('Warning: Detected conda environment: ' + conda_name)
        print('DLL checks will likely report a lot of copies')
        print()

    # Check if user has 32bit python on 64bit Windows
    if machine64bit and not python64bit:
        print('Warning: You are using 32bit python on a 64bit Windows installation')
        print('Please go to https://www.python.org/downloads/windows/ and download the "Windows x86-64 executable installer"')
        print()

    # Check if pip is installed, disabled when running under conda
    if not conda_env and util.find_spec('pip') is None:
        print('Warning: "pip" module could not be found')
        print('Please run [python -c "from urllib import request;exec(request.urlopen(\'https://bootstrap.pypa.io/get-pip.py\').read())"]')
        print('Or manually download "https://bootstrap.pypa.io/get-pip.py" and run [python get-pip.py]')
        print()

    # Check if pefile is installed
    have_pefile = (util.find_spec('pefile') is not None)
    if not have_pefile:
        print('Warning: "pefile" module could not be found, module scans will be limited')
        print('Please run [' + install_cmd + ' install pefile] and rerun this script')
        print()

    # Scan PATH for other python installations
    print('Searching for python installations ...')
    pylocations = find_on_path('python.exe')
    for path in pylocations:
        print('Found python at "' + path + '"')
    if len(pylocations) == 0:
        print('Warning: Failed to find python on the PATH')
        print('Make sure that PATH contains "' + python_location + '"')
    elif len(pylocations) > 1:
        print('Warning: Found multiple python installations')
        print('The current python installation is "' + python_location + '"')

    # Verify all required dependencies are installed and their dependencies
    modules=['numpy', 'cv2', 'torch']
    for module in modules:
        print('\nChecking if "' + module + '" is installed ...')
        spec = util.find_spec(module)
        if spec is not None:
            location = spec.submodule_search_locations
            if location is not None and len(location) > 0:
                if len(location) != 1:
                    print('Warning: Module "' + module + "' was found at multiple locations:")
                    for path in location:
                        print(path)
                else:
                    print('Found "' + module + '" located at "' + location[0] + '"')
                print("Scanning executable files for module ...")
                for path in location:
                    scan_module(path, have_pefile)
            else:
                print('Found "' + module + '" located at "' + spec.origin + '"')
        else:
            print('Error: Could not find "' + module + '"')

    if found_dups:
        print("\nMultiple copies of DLLs were found, but this does not necessarily indicate a problem.")

    if util.find_spec('torch') is not None:
        print("\nGetting torch information ...")
        try:
            import torch
            print("torch.cuda.is_available() returns " + str(torch.cuda.is_available()))
            print("torch.version.cuda = " + str(torch.version.cuda))
            if torch.version.cuda is not None:
                cudapath = find_on_path("nvcuda.dll")
                if len(cudapath) == 0:
                    print('Error: Could not find CUDA')
                    print('Please install cuda from "https://developer.nvidia.com/cuda-downloads?target_os=Windows&target_arch=x86_64"')
        except Exception as e:
            print(e)