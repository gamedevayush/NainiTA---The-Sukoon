"""Delta upload of the iOS Xcode project (owner, 24 Nov 2026: "can whole IPA build process be fast?").

The old way zipped the whole Builds/iOS (2.5 GB -> 1 GB) and uploaded it every time: 24 of the 47 minutes, on a
~5 Mbit/s line. Here the tree is cut into content-defined chunks (~1 MB, cut where the bytes themselves say, so an
insert in level1 only changes the chunks around it). Each chunk is zlib-compressed and named by its hash. Only chunks
the release does not hold yet are uploaded, packed into ~48 MB "pack-*.bin" assets. "delta-manifest.json.gz" says
which chunks make each file and where each chunk sits. The Mac (ios-ipa.yml, mode=delta) runs "pull" to rebuild the
tree byte for byte (every file's sha1 is checked).

  push  [tree]   (Windows, after the Unity iOS build)  default tree: <project>/Builds/iOS
  pull  <outdir> (GitHub's Mac, GH_TOKEN in the environment)
  scan  [tree]   only print what a push would upload (nothing is sent)

Uses numpy for the chunk cuts when it has it (py -3 on this PC); without it, fixed 4 MB chunks (it still works, the
delta is just bigger). The token: GH_TOKEN, else Git's credential store (never printed, never written).
"""
import concurrent.futures as cf
import gzip
import hashlib
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
import zlib

REPO = 'gamedevayush/NainiTA---The-Sukoon'
TAG = 'ios-build'
MANIFEST = 'delta-manifest.json.gz'
MIN_CHUNK = 256 << 10
MAX_CHUNK = 4 << 20
MASK = (1 << 20) - 1          # ~1 MB average
WIN = 64                      # bytes in the rolling window
PACK_MAX = 48 << 20           # compressed bytes per uploaded pack
BLOCK = 16 << 20              # bytes per numpy pass

try:
    import numpy as np
except ImportError:
    np = None

if np is not None:
    # a fixed table (not a seeded RNG, so every numpy gives the same cuts)
    TABLE = np.array([int.from_bytes(hashlib.sha256(bytes([i])).digest()[:8], 'little') for i in range(256)],
                     dtype=np.uint64)


def log(*a):
    print(time.strftime('%H:%M:%S'), *a, flush=True)


def chash(b):
    return hashlib.blake2b(b, digest_size=16).hexdigest()


# ---------------------------------------------------------------- chunking

def cuts_of(path, size):
    """Chunk end offsets of one file."""
    if size <= MIN_CHUNK:
        return [size]
    if np is None:
        return list(range(MAX_CHUNK, size, MAX_CHUNK)) + [size]
    cands = []
    with open(path, 'rb') as f:
        prev = b''
        pos = 0
        while True:
            blk = f.read(BLOCK)
            if not blk:
                break
            buf = prev + blk
            a = np.frombuffer(buf, dtype=np.uint8)
            cs = np.cumsum(TABLE[a], dtype=np.uint64)
            h = cs[WIN:] - cs[:-WIN]                       # sum of the WIN bytes ending at index i + WIN
            hit = np.nonzero((h & np.uint64(MASK)) == 0)[0] + WIN + 1   # cut after that window
            base = pos - len(prev)
            cands.append(hit.astype(np.int64) + base)
            prev = buf[-WIN:]
            pos += len(blk)
    c = np.unique(np.concatenate(cands)) if cands else np.zeros(0, np.int64)
    out, last = [], 0
    while True:
        i = np.searchsorted(c, last + MIN_CHUNK)
        nxt = int(c[i]) if i < len(c) else size
        nxt = min(nxt, last + MAX_CHUNK, size)
        out.append(nxt)
        last = nxt
        if last >= size:
            return out


def scan(tree):
    """files: [rel, size, sha1, [chunk hashes]]; where: chunk -> (path, offset, length) of one copy."""
    files, dirs, where = [], [], {}
    for d, ds, fs in os.walk(tree):
        ds.sort()
        rel_d = os.path.relpath(d, tree).replace('\\', '/')
        if rel_d != '.' and not fs and not ds:
            dirs.append(rel_d)
        for n in sorted(fs):
            p = os.path.join(d, n)
            rel = os.path.relpath(p, tree).replace('\\', '/')
            size = os.path.getsize(p)
            ends = cuts_of(p, size)
            sha = hashlib.sha1()
            hs, off = [], 0
            with open(p, 'rb') as f:
                for e in ends:
                    b = f.read(e - off)
                    sha.update(b)
                    h = chash(b)
                    hs.append(h)
                    where.setdefault(h, (p, off, e - off))
                    off = e
            files.append([rel, size, sha.hexdigest(), hs])
    return files, dirs, where


# ---------------------------------------------------------------- GitHub

def token():
    t = os.environ.get('GH_TOKEN') or os.environ.get('GITHUB_TOKEN')
    if t:
        return t
    try:
        r = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\nusername=gamedevayush\n\n',
                           capture_output=True, text=True, timeout=30)
        for line in r.stdout.splitlines():
            if line.startswith('password='):
                return line[9:]
    except Exception:
        pass
    sys.exit('NO TOKEN')


class GH:
    def __init__(self):
        self.tok = token()

    def req(self, url, data=None, method=None, accept='application/vnd.github+json', ctype=None, tries=4):
        for k in range(tries):
            rq = urllib.request.Request(url, data=data, method=method)
            rq.add_header('Authorization', 'token ' + self.tok)
            rq.add_header('Accept', accept)
            rq.add_header('User-Agent', 'nainita-ios-delta')
            if ctype:
                rq.add_header('Content-Type', ctype)
            try:
                with urllib.request.urlopen(rq, timeout=600) as r:
                    return r.status, r.read()
            except urllib.error.HTTPError as e:
                if e.code in (404, 422) or k == tries - 1:
                    return e.code, e.read()
            except Exception as e:
                if k == tries - 1:
                    raise
                log('  retry', url.split('?')[0][-60:], type(e).__name__)
            time.sleep(3 * (k + 1))

    def json(self, url, **kw):
        st, b = self.req(url, **kw)
        if st >= 300:
            sys.exit(f'GitHub {st} on {url}: {b[:200]!r}')
        return json.loads(b)

    def release(self):
        return self.json(f'https://api.github.com/repos/{REPO}/releases/tags/{TAG}')

    def assets(self, rel_id):
        out, page = [], 1
        while True:
            a = self.json(f'https://api.github.com/repos/{REPO}/releases/{rel_id}/assets?per_page=100&page={page}')
            out += a
            if len(a) < 100:
                return {x['name']: x for x in out}
            page += 1

    def download(self, asset_id):
        """The API answers 302 to a signed storage URL; follow it without the token (the store refuses two auths)."""
        class NoRedirect(urllib.request.HTTPRedirectHandler):
            def redirect_request(self, *a, **k):
                return None
        op = urllib.request.build_opener(NoRedirect)
        for k in range(4):
            try:
                rq = urllib.request.Request(f'https://api.github.com/repos/{REPO}/releases/assets/{asset_id}')
                rq.add_header('Authorization', 'token ' + self.tok)
                rq.add_header('Accept', 'application/octet-stream')
                rq.add_header('User-Agent', 'nainita-ios-delta')
                try:
                    with op.open(rq, timeout=600) as r:
                        return r.read()                      # served directly
                except urllib.error.HTTPError as e:
                    if e.code not in (301, 302, 303, 307, 308):
                        raise
                    loc = e.headers['Location']
                rq2 = urllib.request.Request(loc, headers={'User-Agent': 'nainita-ios-delta'})
                with urllib.request.urlopen(rq2, timeout=600) as r:
                    return r.read()
            except Exception as e:
                if k == 3:
                    raise RuntimeError(f'download {asset_id}: {e}')
                log('  retry download', asset_id, type(e).__name__)
                time.sleep(3 * (k + 1))

    def upload(self, rel_id, name, data, ctype='application/octet-stream'):
        st, b = self.req(f'https://uploads.github.com/repos/{REPO}/releases/{rel_id}/assets?name={name}', data=data,
                         method='POST', ctype=ctype)
        if st != 201:
            raise RuntimeError(f'upload {name}: {st} {b[:200]!r}')

    def delete(self, asset_id):
        self.req(f'https://api.github.com/repos/{REPO}/releases/assets/{asset_id}', method='DELETE')


def remote_manifest(gh, assets):
    a = assets.get(MANIFEST)
    if not a:
        return None
    return json.loads(gzip.decompress(gh.download(a['id'])))


# ---------------------------------------------------------------- push

def push(tree, dry=False):
    t0 = time.time()
    log('scanning', tree, '(numpy cuts)' if np is not None else '(fixed 4 MB cuts: no numpy)')
    files, dirs, where = scan(tree)
    total = sum(f[1] for f in files)
    log(f'scan: {len(files)} files, {total / 2**20:,.0f} MB, {len(where)} unique chunks, {time.time() - t0:.0f} s')

    gh = GH()
    rel = gh.release()
    assets = gh.assets(rel['id'])
    old = remote_manifest(gh, assets) or {'chunks': {}}
    have = {h: v for h, v in old['chunks'].items() if v[0] in assets}     # only chunks whose pack is still there
    new = [h for h in where if h not in have]
    new_raw = sum(where[h][2] for h in new)
    log(f'delta: {len(new)} new chunks, {new_raw / 2**20:,.0f} MB raw (of {total / 2**20:,.0f} MB); '
        f'{len(have)} chunks already on the release')
    if dry:
        return

    # compress the new chunks (zlib frees the GIL: threads run in parallel) and pack them in order
    def comp(h):
        p, off, n = where[h]
        with open(p, 'rb') as f:
            f.seek(off)
            return h, zlib.compress(f.read(n), 6)

    # packs are written to disk one by one and uploaded while the next is made (2 uploads at a time, so a slow
    # request does not leave the line idle); RAM holds one pack plus one batch of chunks at most
    chunks = dict(have)
    tmp = os.path.join(os.path.dirname(os.path.abspath(tree)), '_delta_packs')
    os.makedirs(tmp, exist_ok=True)
    for n in os.listdir(tmp):
        os.remove(os.path.join(tmp, n))
    t1 = time.time()
    sent = {'bytes': 0, 'packs': 0}

    def send(path, ent):
        with open(path, 'rb') as f:
            blob = f.read()
        name = 'pack-' + chash(blob) + '.bin'
        if name not in assets:
            gh.upload(rel['id'], name, blob)
        os.remove(path)
        return name, len(blob), ent

    def done(fut):
        name, n, ent = fut.result()
        for h, v in ent.items():
            chunks[h] = [name] + v
        sent['bytes'] += n
        sent['packs'] += 1
        log(f'  up {name[:17]}  {sent["bytes"] / 2**20:,.0f} MB sent, '
            f'{sent["bytes"] / 2**20 / max(1, time.time() - t1):.2f} MB/s')

    ups = []
    with cf.ThreadPoolExecutor(2) as up, cf.ThreadPoolExecutor(max(2, (os.cpu_count() or 4) - 1)) as zp:
        cur, cur_ent, cur_len, k = None, {}, 0, 0
        for b0 in range(0, len(new), 64):
            for h, z in zp.map(comp, new[b0:b0 + 64]):
                if cur is None:
                    cur_path = os.path.join(tmp, f'p{k:04d}.bin')
                    cur, cur_ent, cur_len, k = open(cur_path, 'wb'), {}, 0, k + 1
                cur.write(z)
                cur_ent[h] = [cur_len, len(z), where[h][2]]
                cur_len += len(z)
                if cur_len >= PACK_MAX:
                    cur.close()
                    while sum(not u.done() for u in ups) >= 3:      # keep at most 3 packs waiting on disk
                        time.sleep(0.5)
                    ups.append(up.submit(send, cur_path, cur_ent))
                    cur = None
        if cur is not None:
            cur.close()
            ups.append(up.submit(send, cur_path, cur_ent))
        for u in ups:
            done(u)
    up_bytes = sent['bytes']

    used = {h for f in files for h in f[3]}
    man = {'version': 1, 'made': time.strftime('%Y-%m-%d %H:%M:%S'), 'files': files, 'dirs': dirs,
           'chunks': {h: chunks[h] for h in used}}
    blob = gzip.compress(json.dumps(man, separators=(',', ':')).encode(), 9)
    if MANIFEST in assets:
        gh.delete(assets[MANIFEST]['id'])
    gh.upload(rel['id'], MANIFEST, blob, 'application/gzip')

    keep = {v[0] for v in man['chunks'].values()}
    gone = [a for n, a in assets.items() if n.startswith('pack-') and n not in keep]
    for a in gone:
        gh.delete(a['id'])
    log(f'PUSH OK: uploaded {up_bytes / 2**20:,.0f} MB in {time.time() - t1:.0f} s, manifest {len(blob) // 1024} KB, '
        f'{len(gone)} old packs removed, total {time.time() - t0:.0f} s')


# ---------------------------------------------------------------- pull

def pull(out):
    t0 = time.time()
    gh = GH()
    rel = gh.release()
    assets = gh.assets(rel['id'])
    man = remote_manifest(gh, assets)
    if not man:
        sys.exit('NO MANIFEST on the release')
    need = sorted({v[0] for v in man['chunks'].values()})
    os.makedirs(out, exist_ok=True)
    cache = os.path.join(out, '..', '_packs')
    os.makedirs(cache, exist_ok=True)

    def get(name):
        p = os.path.join(cache, name)
        if not os.path.exists(p):
            b = gh.download(assets[name]['id'])
            with open(p + '.part', 'wb') as f:
                f.write(b)
            os.replace(p + '.part', p)
        return name

    with cf.ThreadPoolExecutor(8) as ex:
        list(ex.map(get, need))
    log(f'downloaded {len(need)} packs in {time.time() - t0:.0f} s')

    handles = {n: open(os.path.join(cache, n), 'rb') for n in need}
    bad = 0
    for d in man['dirs']:
        os.makedirs(os.path.join(out, d), exist_ok=True)
    for rel_p, size, sha1, hs in man['files']:
        p = os.path.join(out, rel_p)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        s = hashlib.sha1()
        with open(p, 'wb') as f:
            for h in hs:
                pk, off, clen, n = man['chunks'][h]
                fh = handles[pk]
                fh.seek(off)
                b = zlib.decompress(fh.read(clen))
                f.write(b)
                s.update(b)
        if s.hexdigest() != sha1 or os.path.getsize(p) != size:
            log('BAD', rel_p)
            bad += 1
    for fh in handles.values():
        fh.close()
    if bad:
        sys.exit(f'PULL FAILED: {bad} files do not match')
    log(f'PULL OK: {len(man["files"])} files, {sum(f[1] for f in man["files"]) / 2**20:,.0f} MB, '
        f'manifest of {man["made"]}, {time.time() - t0:.0f} s')


if __name__ == '__main__':
    here = os.path.dirname(os.path.abspath(__file__))
    default_tree = os.path.normpath(os.path.join(here, '..', '..', 'Builds', 'iOS'))
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'scan'
    arg = sys.argv[2] if len(sys.argv) > 2 else None
    if cmd == 'push':
        push(arg or default_tree)
    elif cmd == 'scan':
        push(arg or default_tree, dry=True)
    elif cmd == 'pull':
        pull(arg or 'xc')
    else:
        sys.exit(__doc__)
