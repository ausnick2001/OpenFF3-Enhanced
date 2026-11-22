#!/usr/bin/env python3
"""Names the upscaled battle-effect pictures the way the game looks them up and writes them into a mod's textures folder.

Particle pictures (NTPK blocks in the e###.efp packs; the packs name them "<name>.tga", the extension is dropped here) are drawn from  fx.<name>_<W>x<H>.png  or, where the game has two different
pictures with the same name and size,  fx.<name>_<W>x<H>.<crc>.png  (crc = CRC32 of the picture's texel bytes then its palette bytes, 8 hex digits).
Effect model pictures (BTX0 textures) are drawn from  fxm.<name>_<W>x<H>.png.

Input: the folder of upscaled pictures named  <name>_<W>x<H>.png  (a variant of a name: _v1, _v2; a model picture whose name a particle picture
also has: __model after the size), and the vanilla indexes (index.tsv: which game files hold each variant).

  python make-effect-textures.py --upscaled "Upscaled Effects" --index "Vanilla effect textures\\index.tsv" \\
         --efp-dir "<Steam FF3>\\files" --out "<mods>\\FF3 HD Textures\\textures"
"""
import argparse, os, re, shutil, struct, sys, zlib

def u32(d, o): return struct.unpack_from('<I', d, o)[0]

def ntpk_blocks(path):
    d = open(path, 'rb').read()
    i = 0
    while True:
        i = d.find(b'NTPK', i)
        if i < 0: break
        try:
            name = d[i + 12:i + 44].split(b'\0')[0].decode('ascii', 'replace')
            if name.lower().endswith('.tga'): name = name[:-4]      # the packs name their pictures "chinmoku.tga"; the game and these files drop the extension
            texels, pal, ta, pa = u32(d, i + 0x2C), u32(d, i + 0x30), u32(d, i + 0x38), u32(d, i + 0x3C)
            w, h = 8 << d[i + 0x5E], 8 << d[i + 0x5F]
            tx = d[i + ta:i + ta + texels]
            pl = d[i + pa:i + pa + pal] if pa > 0 else b''
            yield name, w, h, zlib.crc32(pl, zlib.crc32(tx)) & 0xFFFFFFFF
        except Exception:
            pass
        i += 4

SIZE = re.compile(r'^(.*)_(\d+)x(\d+)$')

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--upscaled', required=True)
    ap.add_argument('--index', required=True, help='Vanilla effect textures\\index.tsv (particle pictures)')
    ap.add_argument('--efp-dir', required=True)
    ap.add_argument('--out', required=True)
    a = ap.parse_args()

    # index: stem (name_WxH[_vN]) -> first game file that holds it
    holder = {}
    for line in open(a.index, encoding='utf-8').read().splitlines()[1:]:
        cols = line.split('\t')
        if len(cols) >= 4 and cols[3]: holder[cols[0]] = cols[3].split(',')[0].strip()

    cache = {}
    def blocks(efp):
        if efp not in cache: cache[efp] = list(ntpk_blocks(os.path.join(a.efp_dir, efp)))
        return cache[efp]

    os.makedirs(a.out, exist_ok=True)
    made, problems = 0, []
    for f in sorted(os.listdir(a.upscaled)):
        if not f.lower().endswith('.png'): continue
        stem = f[:-4]
        model = stem.endswith('__model')
        if model: stem = stem[:-7]
        variant = re.match(r'^(.*)_v(\d+)$', stem)
        core = variant.group(1) if variant else stem
        m = SIZE.match(core)
        if not m:
            problems.append(f + ': no _WxH in the name'); continue
        name, w, h = m.group(1), int(m.group(2)), int(m.group(3))
        if model:
            target = 'fxm.%s_%dx%d.png' % (name, w, h)
        elif variant:
            efp = holder.get(stem)
            crc = None
            if efp:
                for n, bw, bh, c in blocks(efp):
                    if n == name and bw == w and bh == h: crc = c; break
            if crc is None:
                # not a particle picture: an effect model's (two copies of one picture, the same bytes drawn alike) - v1 serves both
                if variant.group(2) != '1': continue
                target = 'fxm.%s_%dx%d.png' % (name, w, h)
            else:
                target = 'fx.%s_%dx%d.%08x.png' % (name, w, h, crc)
        else:
            target = 'fx.%s_%dx%d.png' % (name, w, h)
        shutil.copyfile(os.path.join(a.upscaled, f), os.path.join(a.out, target))
        made += 1
    print('%d pictures named into %s' % (made, a.out))
    for p in problems: print('PROBLEM', p)
    return 1 if problems else 0

if __name__ == '__main__':
    sys.exit(main())
