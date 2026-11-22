#!/usr/bin/env python3
"""Builds the HD textures download: the zip part(s) and pack.json that the game's "HD textures are not installed" prompt
downloads (OpenFF/Compat/HdTextures.cs).

What goes in
  --mod        the "FF3 HD Textures" mod folder (mod.json, files/, textures/*.png)
  --portraits  the "Retextured Portraits" mod folder (its ff3/files/files/*.NCGR), folded into the same pack
Both end up as ONE mod, "FF3 HD Textures", in the player's mods folder.

How it is made small
  Many textures are the same picture under different map names (d06_01.d06_all_01.png ... d06_05.d06_all_01.png).
  Each distinct picture is stored once; pack.json's "duplicates" lists the other names, and the game copies the file
  back out under each of them when it unpacks. So what is downloaded is about half of what is installed.
  PNGs do not compress, so the zips are stored, not deflated.

What comes out (in --out)
  hd-textures-1.zip, hd-textures-2.zip ...  each under GitHub's 2 GiB per-file limit (--max-part-gb, 1.9 by default)
  pack.json                                  the version, the parts with their sizes and SHA-256, the duplicates
Upload all of them as the assets of one GitHub release (tag "hd-textures"); the game reads
  https://github.com/<owner>/<repo>/releases/download/hd-textures/pack.json

It can be stopped and run again: --budget <seconds> ends a run politely once that much time has gone, and the next run
carries on (the hashes are cached in --out/_hashes.json, finished parts are kept).

Usage
  python Tools/make-texture-pack.py --mod "mods/FF3 HD Textures" --portraits "mods/Retextured Portraits" --out dist/textures
"""
import argparse, hashlib, json, os, sys, time, zipfile

SKIP_WORDS = ('backup', 'annotated', 'copy of', ' - copy')     # stray work files that are not textures
SKIP_NAMES = {'put-pngs-here.txt', 'thumbs.db', 'desktop.ini', '.ds_store', 'pack-installed.json'}   # pack-installed.json: the game's own note of which pack version is installed


def sha256_file(path, chunk=1 << 20):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        while True:
            b = f.read(chunk)
            if not b:
                break
            h.update(b)
    return h.hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--mod', required=True)
    ap.add_argument('--portraits', default=None)
    ap.add_argument('--out', required=True)
    ap.add_argument('--version', default=None, help='the pack\'s version (default: the mod.json\'s)')
    ap.add_argument('--max-part-gb', type=float, default=1.9)
    ap.add_argument('--budget', type=float, default=0, help='stop after about this many seconds; run again to go on (0 = no limit)')
    a = ap.parse_args()
    start = time.time()
    out_of_time = lambda: a.budget > 0 and time.time() - start > a.budget
    os.makedirs(a.out, exist_ok=True)

    # ---- what is in it: (name inside the zip, file on disk)
    entries = []          # everything but the textures
    textures = {}         # key (file name without .png) -> file on disk
    skipped = []
    mod_json = os.path.join(a.mod, 'mod.json')
    for root, _, names in os.walk(a.mod):
        rel_root = os.path.relpath(root, a.mod).replace('\\', '/')
        for n in sorted(names):
            low = n.lower()
            full = os.path.join(root, n)
            rel = n if rel_root == '.' else rel_root + '/' + n
            if rel == 'mod.json' or low in SKIP_NAMES:
                continue
            if any(w in low for w in SKIP_WORDS):
                skipped.append(rel)
                continue
            if rel_root == 'textures' and low.endswith('.png'):
                textures[n[:-4]] = full
            else:
                entries.append((rel, full))
    if a.portraits:
        pdir = os.path.join(a.portraits, 'ff3', 'files', 'files')
        for n in sorted(os.listdir(pdir)):
            if n.lower().endswith('.ncgr'):
                entries.append(('ff3/files/files/' + n, os.path.join(pdir, n)))

    # ---- the merged mod.json
    manifest_in = json.load(open(mod_json, encoding='utf-8'))
    version = a.version or str(manifest_in.get('version', '1.0'))
    manifest_in['version'] = version
    manifest_in['name'] = 'FF3 HD Textures'
    manifest_in['description'] = ('Higher-quality textures and portraits for Final Fantasy III: any PNG in the textures folder is drawn '
                                  'in place of the game\'s texture of the same name, plus the title and menu hand, the name-entry and OK pictures, '
                                  'and the party and profile portraits.')
    mod_json_text = json.dumps(manifest_in, indent=2)

    # ---- which textures are the same picture
    cache_path = os.path.join(a.out, '_hashes.json')
    cache = json.load(open(cache_path)) if os.path.exists(cache_path) else {}
    for key, full in sorted(textures.items()):
        st = os.stat(full)
        sig = '%d:%d' % (st.st_size, int(st.st_mtime))
        c = cache.get(key)
        if not c or c['sig'] != sig:
            cache[key] = {'sig': sig, 'sha': sha256_file(full), 'size': st.st_size}
            if out_of_time():
                json.dump(cache, open(cache_path, 'w'))
                print('time budget reached while hashing (%d of %d); run again' % (sum(1 for k in textures if k in cache), len(textures)))
                return 2
    json.dump(cache, open(cache_path, 'w'))
    groups = {}
    for key in sorted(textures):
        groups.setdefault(cache[key]['sha'], []).append(key)
    stored = {}           # canonical key -> file
    duplicates = {}       # other key -> canonical key
    for keys in groups.values():
        stored[keys[0]] = textures[keys[0]]
        for k in keys[1:]:
            duplicates[k] = keys[0]
    for key, full in sorted(stored.items()):
        entries.append(('textures/' + key + '.png', full))
    installed = sum(os.path.getsize(f) for _, f in entries) + sum(cache[k]['size'] for k in duplicates) + len(mod_json_text)
    print('%d textures, %d stored once, %d copies; %d other files; skipped: %s' % (len(textures), len(stored), len(duplicates), len(entries) - len(stored), ', '.join(skipped) or 'none'))

    # ---- the parts: greedy, in name order, each under the limit (mod.json goes in part 1)
    limit = int(a.max_part_gb * 1e9)
    parts = [[('mod.json', None)]]
    used = [len(mod_json_text)]
    for name, full in sorted(entries):
        size = os.path.getsize(full)
        if used[-1] + size > limit and len(parts[-1]) > 0:
            parts.append([]); used.append(0)
        parts[-1].append((name, full)); used[-1] += size

    # ---- write them (a finished part is kept; an unfinished one carries on)
    infos = []
    for i, items in enumerate(parts, 1):
        zip_name = 'hd-textures-%d.zip' % i
        zip_path = os.path.join(a.out, zip_name)
        done_marker = zip_path + '.done'
        # What this part should hold, as a signature: a part left from other inputs (the textures changed since) is started again.
        sig = hashlib.sha256('\n'.join('%s:%d' % (n, len(mod_json_text) if f is None else os.path.getsize(f)) for n, f in items).encode()).hexdigest()
        sig_path = zip_path + '.sig'
        if not os.path.exists(sig_path) or open(sig_path).read() != sig:
            for stale in (zip_path, done_marker):
                if os.path.exists(stale):
                    os.remove(stale)
            open(sig_path, 'w').write(sig)
        if not os.path.exists(done_marker):
            have = set()
            if os.path.exists(zip_path):
                try:
                    with zipfile.ZipFile(zip_path) as z:
                        have = set(z.namelist())
                except zipfile.BadZipFile:
                    os.remove(zip_path)
            with zipfile.ZipFile(zip_path, 'a' if have else 'w', zipfile.ZIP_STORED, allowZip64=True) as z:
                for name, full in items:
                    if name in have:
                        continue
                    if full is None:
                        z.writestr(name, mod_json_text)
                    else:
                        z.write(full, name)
                    if out_of_time():
                        print('time budget reached in %s; run again' % zip_name)
                        return 2
            open(done_marker, 'w').write('done')
        infos.append({'name': zip_name, 'size': os.path.getsize(zip_path), 'sha256': sha256_file(zip_path)})
        print('%s: %d files, %.1f MB' % (zip_name, len(items), infos[-1]['size'] / 1e6))
        if out_of_time() and i < len(parts):
            print('time budget reached; run again')
            return 2

    pack = {
        'version': version,
        'folder': 'FF3 HD Textures',
        'size': sum(p['size'] for p in infos),
        'installedSize': installed,
        'parts': infos,
        'duplicates': dict(sorted(duplicates.items())),
    }
    json.dump(pack, open(os.path.join(a.out, 'pack.json'), 'w', encoding='utf-8'), indent=1)
    print('pack.json written: download %.2f GB, installed %.2f GB, %d parts' % (pack['size'] / 1e9, installed / 1e9, len(infos)))
    for p in infos:
        open(os.path.join(a.out, p['name'] + '.sha256'), 'w').write(p['sha256'] + '  ' + p['name'] + '\n')
    return 0


if __name__ == '__main__':
    sys.exit(main())
