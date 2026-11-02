"""One-client localhost UDP impairment proxy; standard library only, not game code.

python tools/udp_proxy.py --listen 7778 --target 7777 --delay-ms 75 --jitter-ms 15 --loss .03
Connect ONE client to 127.0.0.1:7778; host listens on 7777. Use another proxy port for each additional client.
Both directions are delayed, so base added RTT is 150ms. Ctrl+C stops the proxy.
"""
import argparse
import heapq
import random
import select
import socket
import time

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--listen', type=int, default=7778)
p.add_argument('--target', type=int, default=7777)
p.add_argument('--delay-ms', type=float, default=75)
p.add_argument('--jitter-ms', type=float, default=15)
p.add_argument('--loss', type=float, default=.03)
p.add_argument('--seed', type=int, default=480)
a = p.parse_args()
if not 0 <= a.loss <= 1 or a.delay_ms < 0 or a.jitter_ms < 0:
    p.error('loss must be [0,1]; delay and jitter must be nonnegative')
rng = random.Random(a.seed)
front = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
front.bind(('127.0.0.1', a.listen))
back = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
back.bind(('127.0.0.1', 0))
if hasattr(socket, 'SIO_UDP_CONNRESET'):
    # Windows delivers ICMP port-unreachable as a recv error during host startup.
    front.ioctl(socket.SIO_UDP_CONNRESET, False)
    back.ioctl(socket.SIO_UDP_CONNRESET, False)
target = ('127.0.0.1', a.target)
client = None
queue = []
sequence = dropped = forwarded = 0
print(f'PROXY_READY {a.listen} -> {a.target}; RTT +{a.delay_ms*2}ms; loss {a.loss}', flush=True)
try:
    while True:
        now = time.monotonic()
        timeout = max(0, min(.02, queue[0][0]-now)) if queue else .02
        readable, _, _ = select.select([front, back], [], [], timeout)
        for source in readable:
            try:
                data, address = source.recvfrom(65535)
            except ConnectionResetError:
                # An ICMP error is not the end of this UDP relay's lifetime.
                continue
            if source is front:
                if client is not None and client != address:
                    continue
                client = address
                destination, peer = back, target
            else:
                if address != target or client is None:
                    continue
                destination, peer = front, client
            if rng.random() < a.loss:
                dropped += 1
                continue
            sequence += 1
            delay = max(0, a.delay_ms+rng.uniform(-a.jitter_ms,a.jitter_ms))/1000
            heapq.heappush(queue, (time.monotonic()+delay, sequence, destination, peer, data))
        while queue and queue[0][0] <= time.monotonic():
            _, _, destination, peer, data = heapq.heappop(queue)
            destination.sendto(data, peer)
            forwarded += 1
except KeyboardInterrupt:
    print(f'PROXY_STOP forwarded={forwarded} dropped={dropped}', flush=True)
finally:
    front.close()
    back.close()
