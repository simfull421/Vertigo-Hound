public enum HitSfxType
{
    Body,
    Head
}

public interface IHitAudioService
{
    void PlayHitAudio(HitSfxType hitType);
}
