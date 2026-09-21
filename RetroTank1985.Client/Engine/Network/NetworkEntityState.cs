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

        // หากความคลาดเคลื่อนสูงมาก (> 16px เช่น เกิดใหม่ โดนชนกระเด็น หรือวาร์ป) ให้ Hard Snap พิกัดตาม Host ทันที
        if (dx > 16f || dy > 16f)
        {
            CurrentX = hostX;
            CurrentY = hostY;
            return (hostX, hostY);
        }

        // หากความคลาดเคลื่อนต่ำมาก (< 1.5px) ให้เชื่อ Local Prediction 100% เพื่อไม่ให้เกิดอาการเลื่อนหรือหลุดล็อก Grid
        if (dx <= 1.5f && dy <= 1.5f)
        {
            CurrentX = predictedX;
            CurrentY = predictedY;
            return (predictedX, predictedY);
        }

        // หากคลาดเคลื่อนระดับกลาง (1.5px - 16px จาก Latency) ให้ค่อยๆ ดึงเข้าหา Host แบบคง Snap Grid
        float reconciledX = predictedX + (hostX - predictedX) * 0.20f;
        float reconciledY = predictedY + (hostY - predictedY) * 0.20f;

        // บังคับ Snap 8px Grid หากแกนใดแกนหนึ่งใกล้เส้นทางเดิน 8px
        float gridX = MathF.Round(reconciledX / 8f) * 8f;
        float gridY = MathF.Round(reconciledY / 8f) * 8f;
        if (MathF.Abs(reconciledX - gridX) < 0.5f) reconciledX = gridX;
        if (MathF.Abs(reconciledY - gridY) < 0.5f) reconciledY = gridY;

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
