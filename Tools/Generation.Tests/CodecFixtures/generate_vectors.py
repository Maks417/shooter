"""Independent P02 codec vectors from numeric diagnostic construction, not shipping art."""
import hashlib
import struct

p = lambda *v: struct.pack('<' + 'f' * len(v), *v)
mesh = struct.pack('<IIII', 3, 3, 1, 0)
for i in range(3):
    x, y = int(i == 1), int(i == 2)
    mesh += p(x, y, 0, 0, 0, 1, 1, 0, 0, 1, x, y)
mesh += struct.pack('<IIIIIH', 0, 1, 2, 0, 3, 0)
envelope = b'SGAF' + struct.pack('<IIII', 1, 1, 0, len(mesh))
envelope += hashlib.sha256(mesh).digest() + bytes(32) + bytes.fromhex('11' * 32) + mesh
print('meshHex=' + mesh.hex())
print('meshSha256=' + hashlib.sha256(mesh).hexdigest())
print('envelopeSha256=' + hashlib.sha256(envelope).hexdigest())
print('envelopeLength=' + str(len(envelope)))
