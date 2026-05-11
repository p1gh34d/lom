using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HitButtonController : MonoBehaviour
{
    [SerializeField] private GameObject[] buttonPrefabs;
    [SerializeField] private GameObject flameEffectPrefab; // Префаб огонька
    [SerializeField] private GameObject sparkEffectPrefab; // Префаб искр для длинных нот

    private GameObject[] hitButtons = new GameObject[5];
    private Transform[] cylinders = new Transform[5];
    private Transform[] spheres = new Transform[5];
    private Vector3[] cylinderOriginalPositions = new Vector3[5];
    private Vector3[] sphereOriginalPositions = new Vector3[5];
    private (KeyCode key, int index)[] keyMappings;
    private bool leftyFlip;
    private NoteInputManager noteInputManager;
    private NoteSpawner noteSpawner;
    private int buttonCount;
    private ParticleSystem[] flameEffects;
    private ParticleSystem[] sparkEffects;
private Light[] flameLights;
private Light[] sparkLights;

    private bool[] isLifted = new bool[5];
    private float[] liftAnimationProgress = new float[5];
    private int[] sustainedNoteCount = new int[5];
    private Vector3[] cylinderCurrentPositions = new Vector3[5];
    private Vector3[] sphereCurrentPositions = new Vector3[5];
    private readonly float liftHeight = 0.3f;
    private readonly float pressDepth = 0.15f;
    private readonly float animationDuration = 0.06f;

    public HitButtonController()
    {
        keyMappings = new (KeyCode, int)[5];
    }

    private void Start()
    {
        if (buttonPrefabs.Length != 5)
        {
            Debug.LogError("ButtonPrefabs array must contain exactly 5 elements!");
            return;
        }
        if (flameEffectPrefab == null)
        {
            Debug.LogError("flameEffectPrefab is not assigned!");
            return;
        }
        if (sparkEffectPrefab == null)
        {
            Debug.LogError("sparkEffectPrefab is not assigned!");
            return;
        }

        leftyFlip = PlayerPrefs.GetInt("LeftyFlip", 0) == 1;
        noteInputManager = FindObjectOfType<NoteInputManager>();
        if (noteInputManager == null)
        {
            Debug.LogError("NoteInputManager not found in scene!");
            return;
        }
        noteSpawner = FindObjectOfType<NoteSpawner>();
        if (noteSpawner == null)
        {
            Debug.LogError("NoteSpawner not found in scene!");
            return;
        }

        string selectedDifficulty = PlayerPrefs.GetString("SelectedDifficulty", "ExpertSingle");
        bool useFourFrets = PlayerPrefs.GetInt("UseFourFrets", 0) == 1 && 
                           (selectedDifficulty == "EasySingle" || selectedDifficulty == "MediumSingle");
        buttonCount = useFourFrets ? 4 : 5;

        hitButtons = new GameObject[buttonCount];
        cylinders = new Transform[buttonCount];
        spheres = new Transform[buttonCount];
        cylinderOriginalPositions = new Vector3[buttonCount];
        sphereOriginalPositions = new Vector3[buttonCount];
        keyMappings = new (KeyCode, int)[buttonCount];
        isLifted = new bool[buttonCount];
        liftAnimationProgress = new float[buttonCount];
        sustainedNoteCount = new int[buttonCount];
        cylinderCurrentPositions = new Vector3[buttonCount];
        sphereCurrentPositions = new Vector3[buttonCount];
        flameEffects = new ParticleSystem[buttonCount];
        sparkEffects = new ParticleSystem[buttonCount];
        flameLights = new Light[buttonCount];
        sparkLights = new Light[buttonCount];

        SpawnHitButtons();
        SetupKeyMappings();
        noteInputManager.OnNoteHit += HandleNoteHit;
        noteInputManager.OnNoteSustainEnd += HandleNoteSustainEnd;
        noteInputManager.OnNoteSustain += HandleNoteSustain;

        // Ensure enough pixel lights so that all fret lights are visible simultaneously (e.g., chords with 3+ notes)
        QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, buttonCount);

        for (int i = 0; i < buttonCount; i++)
        {
            cylinderCurrentPositions[i] = cylinderOriginalPositions[i];
            sphereCurrentPositions[i] = sphereOriginalPositions[i];
        }

        UserManager.Instance.OnUserChanged += OnUserChanged;

        // Первый Play() партиклов инициализирует симуляцию — разогреваем в меню загрузки, а не на первой ноте
        PrewarmHitVfx();
    }

    private void PrewarmHitVfx()
    {
        for (int i = 0; i < buttonCount; i++)
        {
            if (flameEffects[i] != null)
            {
                flameEffects[i].Simulate(0f, true, true);
                flameEffects[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (sparkEffects[i] != null)
            {
                sparkEffects[i].Simulate(0f, true, true);
                sparkEffects[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void OnDestroy()
    {
        if (noteInputManager != null)
        {
            noteInputManager.OnNoteHit -= HandleNoteHit;
            noteInputManager.OnNoteSustainEnd -= HandleNoteSustainEnd;
            noteInputManager.OnNoteSustain -= HandleNoteSustain;
        }
        if (UserManager.Instance != null)
        {
            UserManager.Instance.OnUserChanged -= OnUserChanged;
        }
    }

    private void OnUserChanged()
    {
        leftyFlip = PlayerPrefs.GetInt("LeftyFlip", 0) == 1;
        SetupKeyMappings();
    }

    private void SpawnHitButtons()
    {
        Vector3 parentScale = transform.lossyScale;
        float totalWidth = (buttonCount == 4) ? 3.3f : 4f;
        float spacing = totalWidth / (buttonCount - 0.9f);

        for (int i = 0; i < buttonCount; i++)
        {
            int prefabIndex = leftyFlip ? (buttonCount - 1 - i) : i;
            float xPosition = (i * spacing) - (totalWidth / 2.05f);
            Vector3 localPosition = new Vector3(
                xPosition / parentScale.x,
                0.05f / parentScale.y,
                0 / parentScale.z
            );
            Vector3 worldPosition = transform.TransformPoint(localPosition);

            hitButtons[i] = Instantiate(buttonPrefabs[prefabIndex], worldPosition, Quaternion.Euler(5f, 0f, 0f));
            hitButtons[i].transform.localScale = new Vector3(0.465f, 0.5f, 0.4f);

            cylinders[i] = hitButtons[i].transform.Find("Cylinder");
            spheres[i] = hitButtons[i].transform.Find("Sphere");
            if (cylinders[i] != null) cylinderOriginalPositions[i] = cylinders[i].localPosition;
            else Debug.LogWarning($"Cylinder not found in {hitButtons[i].name}");
            if (spheres[i] != null) sphereOriginalPositions[i] = spheres[i].localPosition;
            else Debug.LogWarning($"Sphere not found in {hitButtons[i].name}");

            Vector3 flamePosition = cylinders[i].position + Vector3.up * liftHeight;
            GameObject flameObj = Instantiate(
                flameEffectPrefab, 
                flamePosition, 
                Quaternion.identity, 
                hitButtons[i].transform
            );
            flameObj.transform.localRotation = flameEffectPrefab.transform.localRotation;
            flameEffects[i] = flameObj.GetComponent<ParticleSystem>();
            flameEffects[i].Stop();
flameLights[i] = flameObj.GetComponentInChildren<Light>();
if (flameLights[i] != null)
{
    flameLights[i].enabled = false;
}
else
{
    Debug.LogWarning($"Spot Light not found in flameEffectPrefab for button {i}");
}

            Vector3 sparkPosition = cylinders[i].position + Vector3.up * liftHeight;
            GameObject sparkObj = Instantiate(
                sparkEffectPrefab, 
                sparkPosition, 
                Quaternion.identity, 
                hitButtons[i].transform
            );
            sparkObj.transform.localRotation = sparkEffectPrefab.transform.localRotation;
            sparkEffects[i] = sparkObj.GetComponent<ParticleSystem>();
            sparkEffects[i].Stop();
sparkLights[i] = sparkObj.GetComponentInChildren<Light>();
if (sparkLights[i] != null)
{
    sparkLights[i].enabled = false;
}
else
{
    Debug.LogWarning($"Spot Light not found in sparkEffectPrefab for button {i}");
}
        }
    }

    private void SetupKeyMappings()
    {
        if (leftyFlip)
        {
            if (buttonCount == 4)
            {
                keyMappings[0] = (InputManager.Instance.GetKey("Blue"), 3);
                keyMappings[1] = (InputManager.Instance.GetKey("Yellow"), 2);
                keyMappings[2] = (InputManager.Instance.GetKey("Red"), 1);
                keyMappings[3] = (InputManager.Instance.GetKey("Green"), 0);
            }
            else
            {
                keyMappings[0] = (InputManager.Instance.GetKey("Orange"), 4);
                keyMappings[1] = (InputManager.Instance.GetKey("Blue"), 3);
                keyMappings[2] = (InputManager.Instance.GetKey("Yellow"), 2);
                keyMappings[3] = (InputManager.Instance.GetKey("Red"), 1);
                keyMappings[4] = (InputManager.Instance.GetKey("Green"), 0);
            }
        }
        else
        {
            if (buttonCount == 4)
            {
                keyMappings[0] = (InputManager.Instance.GetKey("Green"), 0);
                keyMappings[1] = (InputManager.Instance.GetKey("Red"), 1);
                keyMappings[2] = (InputManager.Instance.GetKey("Yellow"), 2);
                keyMappings[3] = (InputManager.Instance.GetKey("Blue"), 3);
            }
            else
            {
                keyMappings[0] = (InputManager.Instance.GetKey("Green"), 0);
                keyMappings[1] = (InputManager.Instance.GetKey("Red"), 1);
                keyMappings[2] = (InputManager.Instance.GetKey("Yellow"), 2);
                keyMappings[3] = (InputManager.Instance.GetKey("Blue"), 3);
                keyMappings[4] = (InputManager.Instance.GetKey("Orange"), 4);
            }
        }
    }

    private void Update()
    {
        UpdateButtonStates();
    }

private void HandleNoteHit(int midi, bool isLongNote, NoteInputManager.HitAccuracy accuracy)
{
    if (noteInputManager.UseAccuracySystem && accuracy == NoteInputManager.HitAccuracy.None) return; // Пропускаем только при промахе с включённой системой точности

    int midiIndex = leftyFlip ? (buttonCount == 4 ? 99 - midi : 100 - midi) : midi - 96;

    if (midi == 103) // Аккорд
    {
        for (int i = 0; i < buttonCount; i++)
        {
            if (isLongNote)
            {
                // Для длинных седьмых нот - только искры
                if (sparkEffects[i] != null)
                {
                    sparkEffects[i].Play();
                    if (sparkLights[i] != null)
                    {
                        sparkLights[i].enabled = true;
                    }
                }
            }
            else
            {
                // Для коротких седьмых нот - огонь
                if (flameEffects[i] != null)
                {
                    flameEffects[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    flameEffects[i].Play();
                    if (flameLights[i] != null)
                    {
                        flameLights[i].enabled = true;
                    }
                }
            }
            LiftButton(i, isLongNote);
        }
    }
    else if (midiIndex >= 0 && midiIndex < buttonCount)
    {
        if (flameEffects[midiIndex] != null && !isLongNote)
        {
            flameEffects[midiIndex].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            flameEffects[midiIndex].Play();
            if (flameLights[midiIndex] != null)
            {
                flameLights[midiIndex].enabled = true;
            }
        }
        if (sparkEffects[midiIndex] != null && isLongNote)
        {
            sparkEffects[midiIndex].Play();
            if (sparkLights[midiIndex] != null)
            {
                sparkLights[midiIndex].enabled = true;
            }
        }
        LiftButton(midiIndex, isLongNote);
    }
}

    private void HandleNoteSustain(int midi)
    {
        if (midi == 103) // N 7
        {
            for (int i = 0; i < buttonCount; i++)
            {
                if (sustainedNoteCount[i] > 0 && sparkEffects[i] != null)
                {
                    if (!sparkEffects[i].isPlaying)
                    {
                        sparkEffects[i].Play();
                    }
                }
            }
        }
        else
        {
            int midiIndex = leftyFlip ? (buttonCount == 4 ? 99 - midi : 100 - midi) : midi - 96;
            if (midiIndex >= 0 && midiIndex < buttonCount)
            {
                if (sustainedNoteCount[midiIndex] > 0 && sparkEffects[midiIndex] != null)
                {
                    if (!sparkEffects[midiIndex].isPlaying)
                    {
                        sparkEffects[midiIndex].Play();
                        if (sparkLights[midiIndex] != null)
{
    sparkLights[midiIndex].enabled = true;
}
                    }
                }
            }
        }
    }

    private void HandleNoteSustainEnd(int midi, bool wasInterrupted)
    {
        if (midi == 103) // N 7
        {
            for (int i = 0; i < buttonCount; i++)
            {
                sustainedNoteCount[i]--;
                if (sustainedNoteCount[i] <= 0)
                {
                    sustainedNoteCount[i] = 0;
                    isLifted[i] = false;
                    liftAnimationProgress[i] = 0f;
                    if (sparkEffects[i] != null)
                    {
                        if (sparkEffects[i].isPlaying) sparkEffects[i].Stop();
                    }
                    if (sparkLights[i] != null && sparkLights[i].enabled)
                    {
                        sparkLights[i].enabled = false;
                    }
                }
            }
        }
        else
        {
            int midiIndex = leftyFlip ? (buttonCount == 4 ? 99 - midi : 100 - midi) : midi - 96;
            if (midiIndex >= 0 && midiIndex < buttonCount)
            {
                sustainedNoteCount[midiIndex]--;
                if (sustainedNoteCount[midiIndex] <= 0)
                {
                    sustainedNoteCount[midiIndex] = 0;
                    isLifted[midiIndex] = false;
                    liftAnimationProgress[midiIndex] = 0f;

                    if (sparkEffects[midiIndex] != null)
                    {
                        if (sparkEffects[midiIndex].isPlaying) sparkEffects[midiIndex].Stop();
                    }
                    if (sparkLights[midiIndex] != null && sparkLights[midiIndex].enabled)
                    {
                        sparkLights[midiIndex].enabled = false;
                    }
                }
            }
        }
    }

private void UpdateButtonStates()
{
    // Оптимизация: кэшируем количество объектов для всех циклов
    int spawnedObjectsCount = noteSpawner.SpawnedObjects.Count;
    
    for (int i = 0; i < buttonCount; i++)
    {
        if (hitButtons[i] == null || cylinders[i] == null || spheres[i] == null) continue;

        Vector3 cylinderTargetPosition = cylinderOriginalPositions[i];
        Vector3 sphereTargetPosition = sphereOriginalPositions[i];
        bool isKeyPressed = Input.GetKey(keyMappings[i].key);
        bool isStrumPressed = InputManager.Instance.IsKey("StrumUp") || InputManager.Instance.IsKey("StrumDown");
        bool wasLifted = isLifted[i];

        // Оптимизированная проверка открытых нот и сброс IsSustained одним проходом
        if (sustainedNoteCount[i] > 0)
        {
            bool isOpenNoteOrSeventh = false;
            bool shouldReleaseNotes = false;
            int targetMidi = leftyFlip ? (buttonCount == 4 ? 99 - i : 100 - i) : 96 + i;
            
            // СНАЧАЛА проверяем есть ли открытые ноты для этой кнопки
            for (int j = 0; j < spawnedObjectsCount; j++)
            {
                var noteObj = noteSpawner.SpawnedObjects[j];
                if (noteObj == null) continue;
                var note = noteObj.GetComponent<NoteController>();
                if (note != null && note.IsSustained)
                {
                    if ((note.Midi == targetMidi && note.IsOpen) || note.Midi == 103)
                    {
                        isOpenNoteOrSeventh = true;
                        break;
                    }
                }
            }
            
            // Определяем условие для сброса нот ПОСЛЕ проверки открытых нот
            if (isOpenNoteOrSeventh)
            {
                // Исправление бага: НЕ сбрасываем открытые длинные ноты - они капают очки автоматически!
                shouldReleaseNotes = false;
            }
            else if (!isKeyPressed)
            {
                // Сбрасываем только обычные ноты при отпускании кнопки
                shouldReleaseNotes = true;
            }
            
            // Если нужно сбросить ноты, делаем это одним проходом
            if (shouldReleaseNotes)
            {
                for (int j = 0; j < spawnedObjectsCount; j++)
                {
                    var noteObj = noteSpawner.SpawnedObjects[j];
                    if (noteObj == null) continue;
                    var note = noteObj.GetComponent<NoteController>();
                    if (note != null && note.IsSustained)
                    {
                        bool shouldRelease = false;
                        if (isOpenNoteOrSeventh)
                        {
                            shouldRelease = (note.Midi == targetMidi && note.IsOpen) || note.Midi == 103;
                        }
                        else
                        {
                            shouldRelease = note.Midi == targetMidi;
                        }
                        
                        if (shouldRelease)
                        {
                            note.IsSustained = false;
                            Debug.Log($"Note {note.Midi} released in UpdateButtonStates, IsSustained: {note.IsSustained}");
                        }
                    }
                }
                
                sustainedNoteCount[i] = 0;
                isLifted[i] = false;
                liftAnimationProgress[i] = 0f;
                if (sparkEffects[i] != null && sparkEffects[i].isPlaying)
                {
                    sparkEffects[i].Stop();
                }
                if (sparkLights[i] != null && sparkLights[i].enabled)
                {
                    sparkLights[i].enabled = false;
                }
            }
        }

        // Устанавливаем целевую позицию
        if (sustainedNoteCount[i] > 0)
        {
            cylinderTargetPosition = cylinderOriginalPositions[i] + Vector3.up * liftHeight;
            sphereTargetPosition = sphereOriginalPositions[i] + Vector3.up * liftHeight;
            isLifted[i] = true;
            liftAnimationProgress[i] = 0f;
            cylinders[i].localPosition = cylinderTargetPosition;
            spheres[i].localPosition = sphereTargetPosition;
            cylinderCurrentPositions[i] = cylinderTargetPosition;
            sphereCurrentPositions[i] = sphereTargetPosition;
            continue;
        }
        else if (isLifted[i])
        {
            cylinderTargetPosition = cylinderOriginalPositions[i] + Vector3.up * liftHeight;
            sphereTargetPosition = sphereOriginalPositions[i] + Vector3.up * liftHeight;

            liftAnimationProgress[i] += Time.deltaTime / (animationDuration * 2);
            if (liftAnimationProgress[i] < 0.5f)
            {
                float t = liftAnimationProgress[i] / 0.5f;
                cylinders[i].localPosition = Vector3.Lerp(cylinderCurrentPositions[i], cylinderTargetPosition, t);
                spheres[i].localPosition = Vector3.Lerp(sphereCurrentPositions[i], sphereTargetPosition, t);
            }
            else
            {
                float t = (liftAnimationProgress[i] - 0.5f) / 0.5f;
                cylinders[i].localPosition = Vector3.Lerp(cylinderTargetPosition, cylinderOriginalPositions[i], t);
                spheres[i].localPosition = Vector3.Lerp(sphereTargetPosition, sphereOriginalPositions[i], t);

            }

            if (liftAnimationProgress[i] >= 1f)
            {
                isLifted[i] = false;
                liftAnimationProgress[i] = 0f;
                cylinderCurrentPositions[i] = cylinderOriginalPositions[i];
                sphereCurrentPositions[i] = sphereOriginalPositions[i];
                if (flameLights[i] != null && flameLights[i].enabled)
                {
                    flameLights[i].enabled = false;
                }
            }
            else
            {
                cylinderCurrentPositions[i] = cylinders[i].localPosition;
                sphereCurrentPositions[i] = spheres[i].localPosition;
            }
            continue;
        }
        else if (isKeyPressed)
        {
            cylinderTargetPosition = cylinderOriginalPositions[i] + Vector3.down * pressDepth;
            sphereTargetPosition = sphereOriginalPositions[i] + Vector3.down * pressDepth;
            liftAnimationProgress[i] = Mathf.Clamp01(liftAnimationProgress[i] + Time.deltaTime / animationDuration);
            cylinders[i].localPosition = Vector3.Lerp(cylinderCurrentPositions[i], cylinderTargetPosition, liftAnimationProgress[i]);
            spheres[i].localPosition = Vector3.Lerp(sphereCurrentPositions[i], sphereTargetPosition, liftAnimationProgress[i]);
            cylinderCurrentPositions[i] = cylinders[i].localPosition;
            sphereCurrentPositions[i] = spheres[i].localPosition;
            if (Vector3.Distance(cylinders[i].localPosition, cylinderTargetPosition) < 0.01f)
            {
                liftAnimationProgress[i] = 0f;
            }
        }
        else
        {
            liftAnimationProgress[i] = Mathf.Clamp01(liftAnimationProgress[i] + Time.deltaTime / animationDuration);
            cylinders[i].localPosition = Vector3.Lerp(cylinderCurrentPositions[i], cylinderTargetPosition, liftAnimationProgress[i]);
            spheres[i].localPosition = Vector3.Lerp(sphereCurrentPositions[i], sphereTargetPosition, liftAnimationProgress[i]);
            cylinderCurrentPositions[i] = cylinders[i].localPosition;
            sphereCurrentPositions[i] = spheres[i].localPosition;
            if (Vector3.Distance(cylinders[i].localPosition, cylinderTargetPosition) < 0.01f)
            {
                liftAnimationProgress[i] = 0f;
            }
        }

        // Синхронизируем состояние света с состоянием частиц, чтобы исключить "залипание"
        if (flameLights[i] != null)
        {
            bool flamePlaying = flameEffects[i] != null && flameEffects[i].isPlaying;
            if (flameLights[i].enabled != flamePlaying)
            {
                flameLights[i].enabled = flamePlaying;
            }
        }
        if (sparkLights[i] != null)
        {
            bool sparkPlaying = sparkEffects[i] != null && sparkEffects[i].isPlaying;
            if (sparkLights[i].enabled != sparkPlaying)
            {
                sparkLights[i].enabled = sparkPlaying;
            }
        }
    }
}

    private void LiftButton(int index, bool isLongNote)
    {
        isLifted[index] = true;
        liftAnimationProgress[index] = 0f;
        if (isLongNote)
        {
            sustainedNoteCount[index]++;
        }
        else
        {
            sustainedNoteCount[index] = 0;
        }
    }
}