using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using Unity.Codecs.Opus;
using Unity.Codecs.Opus.Enums;
using System.Collections.Generic;
using System;

public class AudioLoader : MonoBehaviour
{
    public static AudioLoader Instance { get; private set; }

    public static AudioLoader GetOrCreate()
    {
        if (Instance == null)
        {
            var go = new GameObject("AudioLoader");
            Instance = go.AddComponent<AudioLoader>();
            DontDestroyOnLoad(go);
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public IEnumerator LoadSongAsync(string songFolder, string fileName, System.Action<AudioClip> callback)
    {
        string opusPath = Path.Combine(songFolder, $"{fileName}.opus");
        string mp3Path = Path.Combine(songFolder, $"{fileName}.mp3");
        string oggPath = Path.Combine(songFolder, $"{fileName}.ogg");

        if (File.Exists(opusPath))
        {
            try
            {
                AudioClip clip = LoadOpusClip(opusPath);
                callback(clip);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load Opus file {opusPath}: {e.Message}");
                callback(null);
            }
            yield break;
        }
        else if (File.Exists(mp3Path))
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + mp3Path, AudioType.MPEG))
            {
                ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true;
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    callback(clip);
                }
                else
                {
                    Debug.LogError($"Failed to load MP3: {www.error}");
                    callback(null);
                }
            }
        }
        else if (File.Exists(oggPath))
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + oggPath, AudioType.OGGVORBIS))
            {
                ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = true;
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    callback(clip);
                }
                else
                {
                    Debug.LogError($"Failed to load OGG: {www.error}");
                    callback(null);
                }
            }
        }
        else
        {
            Debug.LogWarning($"No audio file (.opus, .mp3 or .ogg) found in {songFolder}");
            callback(null);
        }
    }

    private AudioClip LoadOpusClip(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        int position = 0;

        // Minimal Ogg parser to extract Opus packets
        List<byte[]> opusPackets = new List<byte[]>();
        bool seenOpusHead = false;
        bool skipTags = false;
        int channelCount = 2; // default to stereo if header missing
        int sampleRate = 48000; // Opus outputs 48 kHz

        List<byte> continuedPacket = null;
        bool hasContinuedPacket = false;

        while (position + 27 <= bytes.Length)
        {
            // Check capture pattern "OggS"
            if (!(bytes[position] == (byte)'O' && bytes[position + 1] == (byte)'g' && bytes[position + 2] == (byte)'g' && bytes[position + 3] == (byte)'S'))
            {
                // Not an Ogg page, stop
                break;
            }

            int headerType = bytes[position + 5];
            int segmentCount = bytes[position + 26];
            int segmentTableOffset = position + 27;
            if (segmentTableOffset + segmentCount > bytes.Length)
            {
                break;
            }

            // Read lacing values
            int dataOffset = segmentTableOffset + segmentCount;
            int remaining = bytes.Length - dataOffset;
            if (remaining < 0)
            {
                break;
            }

            // Iterate packets within this page
            int cursor = dataOffset;
            int segIndex = 0;

            // If headerType has continuation flag, then first packet continues
            bool pageContinues = (headerType & 0x01) != 0;
            if (pageContinues && continuedPacket == null)
            {
                // Unexpected continuation without previous data — reset
                pageContinues = false;
            }

            List<byte[]> pagePackets = new List<byte[]>();
            List<byte> building = pageContinues && continuedPacket != null ? continuedPacket : new List<byte>();

            while (segIndex < segmentCount)
            {
                int lacing = bytes[segmentTableOffset + segIndex];
                int toCopy = Math.Min(lacing, bytes.Length - cursor);
                if (toCopy < 0)
                {
                    break;
                }
                if (cursor + toCopy > bytes.Length)
                {
                    break;
                }

                if (toCopy > 0)
                {
                    building.AddRange(new ArraySegment<byte>(bytes, cursor, toCopy));
                    cursor += toCopy;
                }

                segIndex++;

                if (lacing < 255)
                {
                    // packet ends
                    pagePackets.Add(building.ToArray());
                    building = new List<byte>();
                }
            }

            // If last lacing == 255, packet continues on next page
            bool continuesNext = segmentCount > 0 && bytes[segmentTableOffset + segmentCount - 1] == 255 && building.Count > 0;
            continuedPacket = continuesNext ? building : null;
            hasContinuedPacket = continuesNext;

            // Advance to next page
            // Compute total page size: 27 + segmentCount + sum(lacing)
            int payloadSize = 0;
            for (int i = 0; i < segmentCount; i++) payloadSize += bytes[segmentTableOffset + i];
            int pageSize = 27 + segmentCount + payloadSize;
            position += pageSize;

            // Process page packets
            foreach (var pkt in pagePackets)
            {
                if (!seenOpusHead)
                {
                    // Expect OpusHead
                    if (pkt.Length >= 19 && pkt[0] == (byte)'O' && pkt[1] == (byte)'p' && pkt[2] == (byte)'u' && pkt[3] == (byte)'s' &&
                        pkt[4] == (byte)'H' && pkt[5] == (byte)'e' && pkt[6] == (byte)'a' && pkt[7] == (byte)'d')
                    {
                        seenOpusHead = true;
                        channelCount = pkt[9];
                        if (channelCount <= 0) channelCount = 2;
                        // uint inputRate = ReadUInt32LE(pkt, 12); // not used; decoder runs at 48 kHz
                        continue;
                    }
                    else
                    {
                        // Not an Opus file
                        throw new InvalidDataException("Invalid Opus file: OpusHead not found");
                    }
                }

                if (!skipTags)
                {
                    // Skip OpusTags
                    if (pkt.Length >= 8 && pkt[0] == (byte)'O' && pkt[1] == (byte)'p' && pkt[2] == (byte)'u' && pkt[3] == (byte)'s' &&
                        pkt[4] == (byte)'T' && pkt[5] == (byte)'a' && pkt[6] == (byte)'g' && pkt[7] == (byte)'s')
                    {
                        skipTags = true;
                        continue;
                    }
                }

                // Audio packet
                opusPackets.Add(pkt);
            }
        }

        if (!seenOpusHead)
        {
            throw new InvalidDataException("Invalid Opus file: missing OpusHead");
        }

        Channels channelsEnum = channelCount >= 2 ? Channels.Stereo : Channels.Mono;
        List<float> allPcm = new List<float>(1024 * 1024);

        using (OpusDecoder decoder = new OpusDecoder(SamplingRate.Sampling48000, channelsEnum))
        {
            foreach (var packet in opusPackets)
            {
                try
                {
                    float[] pcm = decoder.DecodePacketFloat(packet);
                    if (pcm != null && pcm.Length > 0)
                    {
                        allPcm.AddRange(pcm);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Opus packet decode failed: {ex.Message}");
                }
            }
        }

        float[] pcmData = allPcm.ToArray();
        if (pcmData.Length == 0)
        {
            throw new InvalidDataException("Decoded Opus data is empty");
        }

        int lengthSamples = pcmData.Length / channelCount;
        if (lengthSamples <= 0)
        {
            throw new InvalidDataException("Invalid PCM length after decoding Opus");
        }

        AudioClip clip = AudioClip.Create(Path.GetFileNameWithoutExtension(filePath), lengthSamples, channelCount, sampleRate, false);
        clip.SetData(pcmData, 0);
        return clip;
    }
}