"""Compares two Unity Assets trees file by file (text files with LF endings): python -I tools/setup/diff_trees.py <assets_a> <assets_b>. Reports files only in one tree and files whose content differs."""
import hashlib
import os
import sys

TEXT = ('.cs', '.meta', '.unity', '.prefab', '.mat', '.asset', '.shader', '.json', '.md', '.controller', '.anim', '.asmdef', '.txt', '.lighting', '.hlsl', '.cginc')


def walk(root):
    out = {}
    for dp, _dn, fn in os.walk(root):
        for f in fn:
            p = os.path.join(dp, f)
            out[os.path.relpath(p, root).replace(chr(92), '/')] = p
    return out


def digest(p):
    data = open(p, 'rb').read()
    if p.endswith(TEXT):
        data = data.replace(b'\r\n', b'\n')
    return hashlib.md5(data).hexdigest()


a, b = walk(sys.argv[1]), walk(sys.argv[2])
only_a = sorted(k for k in a if k not in b)
only_b = sorted(k for k in b if k not in a)
diff = sorted(k for k in a if k in b and digest(a[k]) != digest(b[k]))
print(len(a), len(b), 'only in A', len(only_a), 'only in B', len(only_b), 'different', len(diff))
for k in diff[:40]:
    print('DIFF', k)
for k in only_b[:20]:
    print('ONLY B', k)
for k in [k for k in only_a if 'MaterialTest' not in k and 'StreamingAssets' not in k][:20]:
    print('ONLY A', k)
