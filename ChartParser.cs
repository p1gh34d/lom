using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

public class ChartParser : MonoBehaviour
{
    public static List<(int startTick, int duration, float startTime, float endTime, int lastNoteEndTick, float lastNoteEndTime)> StarPowerSections { get; private set; }
    public static List<HashSet<int>> StarPowerSectionUniqueTicks { get; private set; }
    public static List<bool> StarPowerSectionHasNotes { get; private set; }
    public static List<bool> StarPowerSectionLastIsLong { get; private set; }
    public static List<float> StarPowerSectionLastEndTime { get; private set; }

    public static SongData ParseChartFile(string songName, string difficulty)
    {
        SongData chartData = new SongData();
        chartData.header = new Header
        {
            ppq = 192,
            tempos = new List<TempoData>(),
            timeSignatures = new List<TimeSignature>()
        };
        chartData.tracks = new List<Track>();

        // Добавляем значения по умолчанию
        if (chartData.header.tempos.Count == 0)
        {
            chartData.header.tempos.Add(new TempoData { ticks = 0, bpm = 120f });
            //Debug.Log("No tempos found in chart, added default BPM: 120");
        }
        if (chartData.header.timeSignatures.Count == 0)
        {
            chartData.header.timeSignatures.Add(new TimeSignature { ticks = 0, timeSignature = new List<int> { 4, 4 } });
            //Debug.Log("No time signatures found in chart, added default: 4/4");
        }

        string chartText;
        if (songName == "calibration")
        {
            if (Application.isEditor)
            {
                string path = Path.Combine(Application.dataPath, "Resources/Sounds/calibration/notes.chart");
                if (File.Exists(path))
                {
                    chartText = File.ReadAllText(path);
                }
                else
                {
                    Debug.LogError($"Calibration chart file not found at {path}");
                    return null;
                }
            }
            else
            {
                string chartResourcePath = "Sounds/calibration/notes";
                TextAsset chartAsset = Resources.Load<TextAsset>(chartResourcePath);
                if (chartAsset != null)
                {
                    chartText = chartAsset.text;
                }
                else
                {
                    Debug.LogError($"Calibration chart resource not found at {chartResourcePath}");
                    return null;
                }
            }
        }
        else
        {
            string path = Application.isEditor
                ? Path.Combine(Application.dataPath, "songs", songName, "notes.chart")
                : Path.Combine(Directory.GetCurrentDirectory(), "songs", songName, "notes.chart");

            if (!File.Exists(path))
            {
                Debug.LogError($"Файл {path} не найден!");
                return null;
            }
            chartText = File.ReadAllText(path);
        }

        Track currentTrack = new Track { name = difficulty, notes = new List<NoteData>() };
        chartData.tracks.Add(currentTrack);

        string[] lines = chartText.Split('\n');
        string currentSection = "";
        Dictionary<int, List<string>> tickEvents = new Dictionary<int, List<string>>();
        StarPowerSections = new List<(int startTick, int duration, float startTime, float endTime, int lastNoteEndTick, float lastNoteEndTime)>();
        StarPowerSectionUniqueTicks = new List<HashSet<int>>();
        StarPowerSectionHasNotes = new List<bool>();
        StarPowerSectionLastIsLong = new List<bool>();
        StarPowerSectionLastEndTime = new List<float>();

        // Первый проход: собираем все события и Star Power секции
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                continue;
            }

            if (trimmedLine == "{" || trimmedLine == "}") continue;

            string[] parts = trimmedLine.Split(new[] { " = " }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                Debug.LogWarning($"Некорректная строка: {trimmedLine}");
                continue;
            }

            string key = parts[0].Trim();
            string value = parts[1].Trim();

            switch (currentSection)
            {
                case "Song":
                    if (key == "Resolution")
                    {
                        if (int.TryParse(value, out int ppq))
                        {
                            chartData.header.ppq = ppq;
                        }
                    }
                    continue;

                case "SyncTrack":
                    if (!int.TryParse(key, out int tick))
                    {
                        continue;
                    }
                    if (value.StartsWith("B"))
                    {
                        string[] bpmParts = value.Split(' ');
                        if (bpmParts.Length > 1 && int.TryParse(bpmParts[1], out int microBpm))
                        {
                            chartData.header.tempos.Add(new TempoData
                            {
                                ticks = tick,
                                bpm = microBpm / 1000f
                            });
                        }
                    }
                else if (value.StartsWith("TS"))
                {
                    string[] tsParts = value.Split(' ');
                    if (tsParts.Length > 1 && int.TryParse(tsParts[1], out int numerator))
                    {
                        int denominator = 4; // По умолчанию n/4
                        if (tsParts.Length > 2 && int.TryParse(tsParts[2], out int tsType))
                        {
                            if (tsType == 3)
                                denominator = 8; // n/8
                            else if (tsType == 4)
                                denominator = 16; // n/16
                            else if (tsType == 5)
                                denominator = 32; // n/32
                            else if (tsType != 2)
                                Debug.LogWarning($"Неизвестный тип такта TS {tsParts[1]} {tsType}, использую {numerator}/4");
                        }
                        chartData.header.timeSignatures.Add(new TimeSignature
                        {
                            ticks = tick,
                            timeSignature = new List<int> { numerator, denominator }
                        });
                        Debug.Log($"Parsed time signature at tick {tick}: {numerator}/{denominator}");
                    }
                }
                    break;

                case "ExpertSingle":
                case "HardSingle":
                case "MediumSingle":
                case "EasySingle":
                    if (currentSection == difficulty)
                    {
                        if (!int.TryParse(key, out int noteTick))
                        {
                            continue;
                        }
                        if (!tickEvents.ContainsKey(noteTick))
                        {
                            tickEvents[noteTick] = new List<string>();
                        }
                        tickEvents[noteTick].Add(trimmedLine);
                        // Обрабатываем Star Power секции
                        if (value.StartsWith("S 2"))
                        {
                            string[] spParts = value.Split(' ');
                            if (spParts.Length >= 3 && int.TryParse(spParts[2], out int spDuration))
                            {
                                StarPowerSections.Add((noteTick, spDuration, 0f, 0f, noteTick, 0f));
                                //Debug.Log($"Added Star Power section: tick={noteTick}, duration={spDuration}");
                            }
                        }
                    }
                    break;
            }
        }

        // Второй проход: парсим ноты и добавляем их в currentTrack.notes
        foreach (var tickEntry in tickEvents.OrderBy(t => t.Key))
        {
            int tick = tickEntry.Key;
            var events = tickEntry.Value;
            List<NoteData> notesAtTick = new List<NoteData>();
            bool hasForcedMarker = false;

            // Проверяем наличие маркеров forced/tap (N 5 0, N 6 0)
            if (events.Exists(e => e.EndsWith("= N 5 0") || e.EndsWith("= N 6 0")))
            {
                hasForcedMarker = true;
            }

            foreach (string eventLine in events)
            {
                string[] parts = eventLine.Split(new[] { " = " }, StringSplitOptions.None);
                if (parts.Length != 2) continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                if (value.StartsWith("N"))
                {
                    string[] noteParts = value.Split(' ');
                    if (noteParts.Length >= 3 && int.TryParse(noteParts[1], out int noteIndex) &&
                        int.TryParse(noteParts[2], out int duration))
                    {
                        // Пропускаем маркеры forced/tap (N 5 0, N 6 0)
                        if (noteIndex == 5 || noteIndex == 6) continue;

                        int midi;
                        bool isOpen = false;
                        bool isStarPower = StarPowerSections.Any(sp => tick >= sp.startTick && (sp.duration == 0 ? tick == sp.startTick : tick < sp.startTick + sp.duration));
                        if (noteIndex == 7)
                        {
                            midi = 103;
                            isOpen = true;
                        }
                        else
                        {
                            midi = noteIndex + 96;
                            isOpen = noteParts.Length >= 4 && noteParts[3].Trim() == "O" ||
                                     noteParts.Length >= 5 && noteParts[4].Trim() == "O";
                        }

                        float durationInSeconds = CalculateDuration(tick, duration, chartData.header.tempos, chartData.header.ppq);
                        notesAtTick.Add(new NoteData
                        {
                            time = CalculateNoteTime(tick, chartData.header.tempos, chartData.header.ppq),
                            tick = tick,
                            midi = midi,
                            duration = durationInSeconds,
                            forced = hasForcedMarker,
                            isOpen = isOpen,
                            isChord = false,
                            isStarPower = isStarPower
                        });
                        if (isStarPower)
                        {
                            //Debug.Log($"Note at tick={tick}, midi={midi} marked as Star Power");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Некорректный формат ноты: {eventLine}");
                    }
                }
            }

            bool hasOpenEvent = events.Exists(e => e.EndsWith("= E O"));
            int playableNoteCount = notesAtTick.Count;
            foreach (var note in notesAtTick)
            {
                if (hasOpenEvent)
                {
                    note.isOpen = true;
                }
                note.isChord = playableNoteCount > 1;
                currentTrack.notes.Add(note);
            }
        }

        // Логируем все ноты в треке
        //Debug.Log($"All notes in track {difficulty}: {currentTrack.notes.Count}, ticks={string.Join(", ", currentTrack.notes.Select(n => $"tick={n.tick}, duration={n.duration:F4}, isStarPower={n.isStarPower}"))}");

        // Вычисляем время для Star Power секций и последней ноты
        //Debug.Log($"StarPowerSections count before time calculation: {StarPowerSections.Count}");
        for (int i = 0; i < StarPowerSections.Count; i++)
        {
            var spSection = StarPowerSections[i];
            float startTime = CalculateNoteTime(spSection.startTick, chartData.header.tempos, chartData.header.ppq);
            float endTime = CalculateNoteTime(spSection.startTick + spSection.duration, chartData.header.tempos, chartData.header.ppq);
            // Найти последнюю ноту в секции
            int lastNoteEndTick = spSection.startTick;
            float lastNoteEndTime = startTime;
            var sectionNotes = currentTrack.notes
                .Where(n => n.tick >= spSection.startTick && n.tick <= spSection.startTick + spSection.duration)
                .ToList();
            //Debug.Log($"Section {i} notes: {sectionNotes.Count}, ticks={string.Join(", ", sectionNotes.Select(n => $"tick={n.tick}, duration={n.duration:F4}, isStarPower={n.isStarPower}"))}");
            if (sectionNotes.Any())
            {
                var lastNoteEntry = sectionNotes
                    .Select(n => new
                    {
                        Note = n,
                        EndTick = n.tick + (int)(n.duration * chartData.header.ppq / (60f / GetBPMAtTick(n.tick, chartData.header.tempos, chartData.header.ppq)))
                    })
                    .OrderByDescending(x => x.EndTick)
                    .FirstOrDefault();
                if (lastNoteEntry != null)
                {
                    lastNoteEndTick = lastNoteEntry.EndTick;
                    lastNoteEndTime = CalculateNoteTime(lastNoteEndTick, chartData.header.tempos, chartData.header.ppq);
                    //Debug.Log($"Section {i} last note: tick={lastNoteEntry.Note.tick}, duration={lastNoteEntry.Note.duration:F4}, endTick={lastNoteEndTick}, endTime={lastNoteEndTime:F4}s");
                }
            }
            StarPowerSections[i] = (spSection.startTick, spSection.duration, startTime, endTime, lastNoteEndTick, lastNoteEndTime);
            //Debug.Log($"Star Power section {i}: startTick={spSection.startTick}, duration={spSection.duration}, startTime={startTime:F4}s, endTime={endTime:F4}s, lastNoteEndTick={lastNoteEndTick}, lastNoteEndTime={lastNoteEndTime:F4}s");

            // Предварительно кэшируем уникальные тики Star Power нот для секции
            var sectionSPNotes = currentTrack.notes
                .Where(n => n.isStarPower && n.tick >= spSection.startTick && n.tick <= spSection.startTick + spSection.duration)
                .ToList();
            var sectionUniqueTicks = new HashSet<int>(sectionSPNotes.Select(n => n.tick));
            StarPowerSectionUniqueTicks.Add(sectionUniqueTicks);

            // Кэшируем флаги и время окончания последней StarPower ноты
            bool hasSP = sectionSPNotes.Count > 0;
            StarPowerSectionHasNotes.Add(hasSP);
            if (hasSP)
            {
                var lastSpEntry = sectionSPNotes
                    .Select(n => new
                    {
                        Note = n,
                        EndTick = n.tick + (int)(n.duration * chartData.header.ppq / (60f / GetBPMAtTick(n.tick, chartData.header.tempos, chartData.header.ppq)))
                    })
                    .OrderByDescending(x => x.EndTick)
                    .First();
                float lastSpEndTime = CalculateNoteTime(lastSpEntry.EndTick, chartData.header.tempos, chartData.header.ppq);
                StarPowerSectionLastIsLong.Add(lastSpEntry.Note.duration > 0.1f);
                StarPowerSectionLastEndTime.Add(lastSpEndTime);
            }
            else
            {
                StarPowerSectionLastIsLong.Add(false);
                StarPowerSectionLastEndTime.Add(endTime);
            }
        }

        // Сортируем tempos и timeSignatures
        chartData.header.tempos.Sort((a, b) => a.ticks.CompareTo(b.ticks));
        chartData.header.timeSignatures.Sort((a, b) => a.ticks.CompareTo(b.ticks));

        // Вызываем AutoDetectForcedNotes
        foreach (var track in chartData.tracks)
        {
            AutoDetectForcedNotes(track, chartData.header);
        }

        //Debug.Log($"Parsed {StarPowerSections.Count} Star Power sections");
        return chartData;
    }

    private static void AutoDetectForcedNotes(Track track, Header header)
    {
        if (track.notes.Count == 0) return;

        // Кэшируем BPM по тикам
        var bpmCache = new Dictionary<int, float>();
        float currentBPM = header.tempos != null && header.tempos.Count > 0 ? header.tempos[0].bpm : 120f;
        int lastTick = 0;
        foreach (var tempo in header.tempos)
        {
            bpmCache[tempo.ticks] = tempo.bpm;
            currentBPM = tempo.bpm;
            lastTick = tempo.ticks;
        }

        int ticksPerHalfBeat = header.ppq / 2; // Полудоля в тиках (например, 192/2=96)
        bool useExtendedForcedNotes = UserManager.Instance.GetExtendedForcedNotes(); // Чекбокс

        for (int i = 0; i < track.notes.Count; i++)
        {
            NoteData currentNote = track.notes[i];
            if (currentNote.forced || currentNote.isChord) continue; // Пропускаем forced и аккорды

            // Находим предыдущий тик и собираем все ноты на нём
            List<NoteData> prevNotes = null;
            int prevTick = -1;
            float prevBPM = currentBPM;

            // Ищем ближайший предыдущий тик
            for (int j = i - 1; j >= 0; j--)
            {
                if (track.notes[j].tick != prevTick)
                {
                    if (prevTick == -1)
                    {
                        prevTick = track.notes[j].tick;
                        prevNotes = new List<NoteData> { track.notes[j] };
                    }
                    else break;
                }
                else if (prevTick != -1)
                {
                    prevNotes.Add(track.notes[j]);
                }
            }

            if (prevNotes != null && prevTick >= 0)
            {
                int tickDiff = currentNote.tick - prevTick;

                // Получаем BPM для предыдущего тика (оптимизировано без LINQ)
                foreach (var bpmEntry in bpmCache)
                {
                    if (bpmEntry.Key <= prevTick)
                    {
                        prevBPM = bpmEntry.Value;
                    }
                    else break;
                }

                // Учитываем настройку расширенного диапазона
                int effectiveTicksPerHalfBeat = ticksPerHalfBeat;
                if (useExtendedForcedNotes && prevBPM >= 165f)
                {
                    effectiveTicksPerHalfBeat *= 2; // Удваиваем диапазон (например, 96 → 192)
                }

                if (tickDiff < effectiveTicksPerHalfBeat)
                {
                    foreach (var prevNote in prevNotes)
                    {
                        if (prevNote.midi != currentNote.midi)
                        {
                            currentNote.forced = true;
                            break;
                        }
                    }
                }
            }
        }
    }

    public static float GetBPMAtTick(float time, List<TempoData> tempos, int ppq)
    {
        float currentBPM = tempos != null && tempos.Count > 0 ? tempos[0].bpm : 120f;
        foreach (var tempo in tempos)
        {
            if (tempo.ticks <= time)
            {
                currentBPM = tempo.bpm;
            }
            else break;
        }
        return currentBPM;
    }

    private static TimeSignature GetTimeSignatureAtTick(float time, List<TimeSignature> timeSignatures, List<TempoData> tempos, int ppq)
    {
        if (timeSignatures == null || timeSignatures.Count == 0)
        {
            return new TimeSignature { ticks = 0, timeSignature = new List<int> { 4, 4 } }; // По умолчанию 4/4
        }

        TimeSignature currentSignature = timeSignatures[0];
        // Оптимизировано - предполагаем, что timeSignatures уже отсортированы
        foreach (var ts in timeSignatures)
        {
            if (ts.ticks <= time)
            {
                currentSignature = ts;
            }
            else break;
        }
        return currentSignature;
    }

    public static float CalculateNoteTime(float tick, List<TempoData> tempos, int ppq)
    {
        float time = 0f;
        float lastTick = 0f;
        float currentBPM = tempos != null && tempos.Count > 0 ? tempos[0].bpm : 120f;

        if (tempos != null && tempos.Count > 0)
        {
            foreach (var tempo in tempos)
            {
                if (tempo.ticks <= tick)
                {
                    float deltaTicks = tempo.ticks - lastTick;
                    time += (deltaTicks / ppq) * (60f / currentBPM);
                    lastTick = tempo.ticks;
                    currentBPM = tempo.bpm;
                }
                else break;
            }
        }

        float remainingTicks = tick - lastTick;
        time += (remainingTicks / ppq) * (60f / currentBPM);

        return time;
    }

    private static float CalculateDuration(float startTick, int durationTicks, List<TempoData> tempos, int ppq)
    {
        if (durationTicks <= 0) return 0f;

        float startTime = CalculateNoteTime(startTick, tempos, ppq);
        float endTime = CalculateNoteTime(startTick + durationTicks, tempos, ppq);

        return endTime - startTime;
    }
}