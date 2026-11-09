"""Send a Python script to the installed Blender Lab MCP bridge (localhost only)."""
import argparse
import json
import socket
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--file')
parser.add_argument('--code')
parser.add_argument('--timeout', type=float, default=50)
args = parser.parse_args()
code = Path(args.file).read_text(encoding='utf-8') if args.file else args.code
if not code:
    parser.error('Supply --file or --code')
request = {'type': 'execute', 'strict_json': True, 'code': code}
with socket.create_connection(('127.0.0.1', 9876), timeout=5) as client:
    client.settimeout(args.timeout)
    client.sendall(json.dumps(request).encode('utf-8') + b'\0')
    data = bytearray()
    while b'\0' not in data:
        block = client.recv(65536)
        if not block:
            raise RuntimeError('Incomplete response from Blender')
        data.extend(block)
    reply = json.loads(bytes(data).split(b'\0', 1)[0])
    # Export operators print a lot of routine output; retain diagnostics, not chatter.
    reply.pop('stdout', None)
    print(json.dumps(reply, ensure_ascii=False, indent=2))
    if reply.get('status') != 'ok':
        raise SystemExit(1)
