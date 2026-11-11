import asyncio
import json
import ssl
import struct
import sys
import time
import uuid
from xml.sax.saxutils import escape

import aiohttp
import certifi
from edge_tts.constants import SEC_MS_GEC_VERSION, WSS_HEADERS, WSS_URL
from edge_tts.drm import DRM

SSL_CTX = ssl.create_default_context(cafile=certifi.where())
AUDIO_FORMAT = "audio-24khz-48kbitrate-mono-mp3"
session = None
websocket = None


def write_frame(payload):
    sys.stdout.buffer.write(struct.pack(">I", len(payload)))
    sys.stdout.buffer.write(payload)
    sys.stdout.buffer.flush()


def date_to_string():
    return time.strftime(
        "%a %b %d %Y %H:%M:%S GMT+0000 (Coordinated Universal Time)",
        time.gmtime(),
    )


def connect_id():
    return uuid.uuid4().hex


def locale_of(voice):
    parts = (voice or "").split("-")
    if len(parts) >= 2:
        return parts[0] + "-" + parts[1]
    return "en-US"


def clean_text(text):
    chars = []
    for char in text:
        code = ord(char)
        if (0 <= code <= 8) or (11 <= code <= 12) or (14 <= code <= 31):
            chars.append(" ")
        else:
            chars.append(char)
    return "".join(chars)


def make_ssml(text, voice):
    return (
        "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='"
        + locale_of(voice)
        + "'>"
        "<voice name='" + voice + "'>"
        "<prosody pitch='+0Hz' rate='+0%' volume='+0%'>"
        + escape(clean_text(text))
        + "</prosody></voice></speak>"
    )


def parse_binary(message):
    if len(message) < 2:
        return {}, b""
    header_length = int.from_bytes(message[:2], "big")
    headers = {}
    for line in message[:header_length].split(b"\r\n"):
        if b":" not in line:
            continue
        key, value = line.split(b":", 1)
        headers[key] = value
    return headers, message[header_length + 2 :]


async def close_ws():
    global session, websocket
    if websocket is not None:
        try:
            await websocket.close()
        except Exception:
            pass
    if session is not None:
        try:
            await session.close()
        except Exception:
            pass
    websocket = None
    session = None


async def connect_ws():
    global session, websocket
    await close_ws()
    timeout = aiohttp.ClientTimeout(total=None, connect=None, sock_connect=10, sock_read=60)
    session = aiohttp.ClientSession(timeout=timeout, trust_env=True)
    url = (
        WSS_URL
        + "&ConnectionId="
        + connect_id()
        + "&Sec-MS-GEC="
        + DRM.generate_sec_ms_gec()
        + "&Sec-MS-GEC-Version="
        + SEC_MS_GEC_VERSION
    )
    websocket = await session.ws_connect(
        url,
        compress=15,
        headers=DRM.headers_with_muid(WSS_HEADERS),
        ssl=SSL_CTX,
    )
    await websocket.send_str(
        "X-Timestamp:" + date_to_string() + "\r\n"
        "Content-Type:application/json; charset=utf-8\r\n"
        "Path:speech.config\r\n\r\n"
        '{"context":{"synthesis":{"audio":{"metadataoptions":'
        '{"sentenceBoundaryEnabled":"false","wordBoundaryEnabled":"false"},'
        '"outputFormat":"' + AUDIO_FORMAT + '"}}}}\r\n'
    )


async def synth(text, voice, retry=True):
    global websocket
    if websocket is None or websocket.closed:
        await connect_ws()
    await websocket.send_str(
        "X-RequestId:" + connect_id() + "\r\n"
        "Content-Type:application/ssml+xml\r\n"
        "X-Timestamp:" + date_to_string() + "Z\r\n"
        "Path:ssml\r\n\r\n"
        + make_ssml(text, voice)
    )
    audio = bytearray()
    async for received in websocket:
        if received.type == aiohttp.WSMsgType.TEXT:
            encoded = received.data.encode("utf-8")
            split = encoded.find(b"\r\n\r\n")
            path = b""
            if split >= 0:
                for line in encoded[:split].split(b"\r\n"):
                    if line.startswith(b"Path:"):
                        path = line.split(b":", 1)[1]
            if path == b"turn.end":
                break
        elif received.type == aiohttp.WSMsgType.BINARY:
            headers, body = parse_binary(received.data)
            if headers.get(b"Path") == b"audio" and body:
                audio.extend(body)
        elif received.type in (aiohttp.WSMsgType.CLOSE, aiohttp.WSMsgType.CLOSED, aiohttp.WSMsgType.ERROR):
            if retry:
                await connect_ws()
                return await synth(text, voice, False)
            break
    if not audio and retry:
        await connect_ws()
        return await synth(text, voice, False)
    return bytes(audio)


async def main():
    try:
        await connect_ws()
    except Exception as exc:
        sys.stderr.write(str(exc) + "\n")
        sys.stderr.flush()
        await close_ws()
    sys.stdout.buffer.write(b"READY\n")
    sys.stdout.buffer.flush()
    loop = asyncio.get_running_loop()
    while True:
        line = await loop.run_in_executor(None, sys.stdin.buffer.readline)
        if not line:
            break
        try:
            req = json.loads(line.decode("utf-8"))
            if req.get("cmd") == "quit":
                break
            text = (req.get("text") or "").strip()
            voice = req.get("voice") or "en-US-AvaNeural"
            if not text:
                write_frame(b"")
                continue
            write_frame(await synth(text, voice))
        except Exception as exc:
            sys.stderr.write(str(exc) + "\n")
            sys.stderr.flush()
            try:
                await connect_ws()
            except Exception:
                await close_ws()
            write_frame(b"")
    await close_ws()


if __name__ == "__main__":
    asyncio.run(main())
