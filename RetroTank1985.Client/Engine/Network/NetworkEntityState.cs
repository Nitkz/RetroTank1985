namespace RetroTank1985.Client.Engine.Network;

/// <summary>
/// โครงสร้างสำหรับเก็บประวัติพิกัดตำแหน่งของ Entity เพื่อใช้ทำ Smooth Lerp Interpolation
/// </summary>
public struct NetworkEntityState
{
    public float TargetX;
    public float TargetY;
    public float CurrentX;
    public float CurrentY;
    public bool Initialized;

    public void UpdateTarget(float newX, float newY, bool snapImmediately = false)
    {
        TargetX = newX;
        TargetY = newY;

        if (!Initialized || snapImmediately)
        {
            CurrentX = newX;
            CurrentY = newY;
            Initialized = true;
            return;
        }

        // หากระยะห่างกระโดดไกลเกินไป (เช่น Respawn หรือ Teleport > 24px) ให้ Snap ทันที
        float dx = Math.Abs(newX - CurrentX);
        float dy = Math.Abs(newY - CurrentY);
        if (dx > 24f || dy > 24f)
        {
            CurrentX = newX;
            CurrentY = newY;
        }
    }

    /// <summary>
    /// ทำ Smooth Lerp อิงตาม Frame Delta Time (อัตรา 0.40 ต่อ 60Hz tick ให้ความลื่นไหลแต่ยังคมชัด)
    /// </summary>
    public void Interpolate(float lerpFactor = 0.40f)
    {
        if (!Initialized) return;

        CurrentX += (TargetX - CurrentX) * lerpFactor;
        CurrentY += (TargetY - CurrentY) * lerpFactor;

        // Snapping ระยะใกล้มาก (< 0.1px) เพื่อประหยัด CPU และความแม่นยำของพิกเซล 8-bit
        if (Math.Abs(TargetX - CurrentX) < 0.08f) CurrentX = TargetX;
        if (Math.Abs(TargetY - CurrentY) < 0.08f) CurrentY = TargetY;
    }

    public void Reset()
    {
        Initialized = false;
        TargetX = 0;
        TargetY = 0;
        CurrentX = 0;
        CurrentY = 0;
    }
}
