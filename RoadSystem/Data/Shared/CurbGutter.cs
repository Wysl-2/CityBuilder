[System.Serializable]
public struct CurbGutter
{
    public float skirtOut;
    public float skirtDown;
    public float gutterDepth;
    public float gutterWidth;

    public static CurbGutter Default() => new CurbGutter
    {
        skirtOut = 0.35f,
        skirtDown = 0.05f,
        gutterDepth = 0.5f,
        gutterWidth = 0.5f
    };
}