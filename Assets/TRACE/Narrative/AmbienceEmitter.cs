using UnityEngine;

namespace TRACE.Narrative
{
    // A placed environmental loop (broken vent, powered terminal, pipe, panel): a 3D AudioSource that fades in from a
    // random point of its loop with a tiny pitch offset, so two emitters of the same clip never phase together.
    // Fades on unscaled time; SetLevel lets the story duck it without a hard stop.
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class AmbienceEmitter : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField, Min(0.05f)] private float fadeIn = 1.5f;
        [SerializeField, Range(0f, 0.05f)] private float pitchSpread = 0.02f;
        private AudioSource source;
        private float level = 1f;
        public float Volume => source != null ? source.volume : 0f;
        public float Target => volume * level;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.pitch = 1f + Random.Range(-pitchSpread, pitchSpread);
        }

        private void Start()
        {
            if (source.clip == null) return;
            source.time = Random.Range(0f, source.clip.length * 0.95f);
            source.Play();
        }

        public void SetLevel(float value) => level = Mathf.Clamp01(value);

        private void Update()
        {
            if (source.clip == null) return;
            source.volume = Mathf.MoveTowards(source.volume, Target, Time.unscaledDeltaTime * volume / fadeIn);
        }
    }
}
