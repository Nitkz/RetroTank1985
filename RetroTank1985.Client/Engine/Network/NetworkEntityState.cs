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

    /// <summary>
    /// ปรับสมดุลระหว่าง Local Predicted Position กับ Host Authoritative Position
    /// </summary>
    public (float NewX, float NewY) ReconcilePrediction(float predictedX, float predictedY, float hostX, float hostY)
    {
        if (!Initialized)
        {
            Initialized = true;
            CurrentX = hostX;
            CurrentY = hostY;
            return (hostX, hostY);
        }

        float dx = Math.Abs(hostX - predictedX);
        float dy = Math.Abs(hostY - predictedY);

        // กรณีความคลาดเคลื่อนสูงมาก (> 20px เช่น เกิดใหม่ โดนชนกระเด็น หรือวาร์ป) ให้ Hard Snap พิกัดตาม Host ทันที
        if (dx > 20f || dy > 20f)
        {
            CurrentX = hostX;
            CurrentY = hostY;
            return (hostX, hostY);
        }

        // กรณีความคลาดเคลื่อนเล็กน้อย (1-4px จาก Latency) ให้ค่อยๆ นวดตำแหน่ง (Smooth blend 25% error correction)
        float reconciledX = predictedX + (hostX - predictedX) * 0.25f;
        float reconciledY = predictedY + (hostY - predictedY) * 0.25f;

        CurrentX = reconciledX;
        CurrentY = reconciledY;
        return (reconciledX, reconciledY);
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
