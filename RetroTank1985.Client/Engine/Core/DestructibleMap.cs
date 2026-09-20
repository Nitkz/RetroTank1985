using RetroTank1985.Client.Engine.Enums;
using RetroTank1985.Client.Engine.Models;

namespace RetroTank1985.Client.Engine.Core;

public interface IDestructibleMap
{
    const int GridDimension = 26; // 26x26 8px sub-tiles
    const float PlayfieldSize = 208f; // 26 * 8 = 208 px

    bool IsDirty { get; set; }
    bool IsEagleDestroyed { get; }

    void LoadStage(List<List<int>>? stageGrid);
    bool CanTankMoveTo(float x, float y, float size = 16f);
    SubTileType GetSubTile(int row, int col);
    void SetSubTile(int row, int col, SubTileType type);
    bool HandleBulletHit(Bullet bullet, out AudioSoundEffect soundEffect, out bool hitEagle);
    byte[] GetSubTileBytes();
    void FortifyEagleWithSteel(bool steel);
}

public class DestructibleMap : IDestructibleMap
{
    private readonly SubTileType[,] _grid = new SubTileType[IDestructibleMap.GridDimension, IDestructibleMap.GridDimension];
    private readonly byte[] _flatBytes = new byte[IDestructibleMap.GridDimension * IDestructibleMap.GridDimension];

    public bool IsDirty { get; set; } = true;
    public bool IsEagleDestroyed { get; private set; } = false;

    public void LoadStage(List<List<int>>? stageGrid)
    {
        Array.Clear(_grid, 0, _grid.Length);
        IsEagleDestroyed = false;

        if (stageGrid != null)
        {
            for (int r = 0; r < 13; r++)
            {
                for (int c = 0; c < 13; c++)
                {
                    int tileType = (stageGrid.Count > r && stageGrid[r].Count > c) ? stageGrid[r][c] : 13;
                    int subR = r * 2;
                    int subC = c * 2;

                    switch (tileType)
                    {
                        case 4: // Full Brick
                            SetSubTiles2x2(subR, subC, SubTileType.Brick);
                            break;
                        case 9: // Full Steel
                            SetSubTiles2x2(subR, subC, SubTileType.Steel);
                            break;
                        case 0: // Brick right col
                            _grid[subR, subC + 1] = SubTileType.Brick;
                            _grid[subR + 1, subC + 1] = SubTileType.Brick;
                            break;
                        case 1: // Brick bottom row
                            _grid[subR + 1, subC] = SubTileType.Brick;
                            _grid[subR + 1, subC + 1] = SubTileType.Brick;
                            break;
                        case 2: // Brick left col
                            _grid[subR, subC] = SubTileType.Brick;
                            _grid[subR + 1, subC] = SubTileType.Brick;
                            break;
                        case 3: // Brick top row
                            _grid[subR, subC] = SubTileType.Brick;
                            _grid[subR, subC + 1] = SubTileType.Brick;
                            break;
                        case 5: // Steel right col
                            _grid[subR, subC + 1] = SubTileType.Steel;
                            _grid[subR + 1, subC + 1] = SubTileType.Steel;
                            break;
                        case 6: // Steel bottom row
                            _grid[subR + 1, subC] = SubTileType.Steel;
                            _grid[subR + 1, subC + 1] = SubTileType.Steel;
                            break;
                        case 7: // Steel left col
                            _grid[subR, subC] = SubTileType.Steel;
                            _grid[subR + 1, subC] = SubTileType.Steel;
                            break;
                        case 8: // Steel top row
                            _grid[subR, subC] = SubTileType.Steel;
                            _grid[subR, subC + 1] = SubTileType.Steel;
                            break;
                        case 10: // Water
                            SetSubTiles2x2(subR, subC, SubTileType.Water);
                            break;
                        case 11: // Trees
                            SetSubTiles2x2(subR, subC, SubTileType.Trees);
                            break;
                        case 12: // Ice
                            SetSubTiles2x2(subR, subC, SubTileType.Ice);
                            break;
                    }
                }
            }
        }

        // Eagle base at (24, 12)-(25, 13)
        _grid[24, 12] = SubTileType.Eagle;
        _grid[24, 13] = SubTileType.Eagle;
        _grid[25, 12] = SubTileType.Eagle;
        _grid[25, 13] = SubTileType.Eagle;

        // Eagle Fortress Wall (Π-shape)
        FortifyEagleWithSteel(false);

        IsDirty = true;
    }

    private void SetSubTiles2x2(int subR, int subC, SubTileType type)
    {
        _grid[subR, subC] = type;
        _grid[subR, subC + 1] = type;
        _grid[subR + 1, subC] = type;
        _grid[subR + 1, subC + 1] = type;
    }

    private static readonly (int r, int c)[] EagleWallCoords =
    {
        (23, 11), (23, 12), (23, 13), (23, 14),
        (24, 11), (25, 11),
        (24, 14), (25, 14)
    };

    public void FortifyEagleWithSteel(bool steel)
    {
        var wallType = steel ? SubTileType.Steel : SubTileType.Brick;
        foreach (var (r, c) in EagleWallCoords)
        {
            _grid[r, c] = wallType;
        }
        IsDirty = true;
    }

    public bool CanTankMoveTo(float x, float y, float size = 16f)
    {
        if (x < 0 || x > IDestructibleMap.PlayfieldSize - size) return false;
        if (y < 0 || y > IDestructibleMap.PlayfieldSize - size) return false;

        int minSubC = (int)(x / 8f);
        int maxSubC = (int)((x + size - 0.01f) / 8f);
        int minSubR = (int)(y / 8f);
        int maxSubR = (int)((y + size - 0.01f) / 8f);

        for (int r = minSubR; r <= maxSubR; r++)
        {
            for (int c = minSubC; c <= maxSubC; c++)
            {
                if (r >= 0 && r < IDestructibleMap.GridDimension && c >= 0 && c < IDestructibleMap.GridDimension)
                {
                    var cell = _grid[r, c];
                    if (cell == SubTileType.Brick || cell == SubTileType.Steel ||
                        cell == SubTileType.Water || cell == SubTileType.Eagle ||
                        cell == SubTileType.DestroyedEagle)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    public SubTileType GetSubTile(int row, int col)
    {
        if (row < 0 || row >= IDestructibleMap.GridDimension || col < 0 || col >= IDestructibleMap.GridDimension)
            return SubTileType.Empty;
        return _grid[row, col];
    }

    public void SetSubTile(int row, int col, SubTileType type)
    {
        if (row < 0 || row >= IDestructibleMap.GridDimension || col < 0 || col >= IDestructibleMap.GridDimension)
            return;
        if (_grid[row, col] != type)
        {
            _grid[row, col] = type;
            IsDirty = true;
        }
    }

    public bool HandleBulletHit(Bullet bullet, out AudioSoundEffect soundEffect, out bool hitEagle)
    {
        soundEffect = AudioSoundEffect.None;
        hitEagle = false;

        // Bounding box of 4x4 NES bullet
        const float bSize = 4f;
        int rMin, rMax, cMin, cMax;

        switch (bullet.Direction)
        {
            case Direction.Right:
                cMin = cMax = (int)((bullet.X + bSize - 0.01f) / 8f);
                rMin = (int)(bullet.Y / 8f);
                rMax = (int)((bullet.Y + bSize - 0.01f) / 8f);
                break;
            case Direction.Left:
                cMin = cMax = (int)(bullet.X / 8f);
                rMin = (int)(bullet.Y / 8f);
                rMax = (int)((bullet.Y + bSize - 0.01f) / 8f);
                break;
            case Direction.Down:
                rMin = rMax = (int)((bullet.Y + bSize - 0.01f) / 8f);
                cMin = (int)(bullet.X / 8f);
                cMax = (int)((bullet.X + bSize - 0.01f) / 8f);
                break;
            case Direction.Up:
            default:
                rMin = rMax = (int)(bullet.Y / 8f);
                cMin = (int)(bullet.X / 8f);
                cMax = (int)((bullet.X + bSize - 0.01f) / 8f);
                break;
        }

        if (rMin < 0 || rMax >= IDestructibleMap.GridDimension || cMin < 0 || cMax >= IDestructibleMap.GridDimension)
        {
            return false;
        }

        bool hitOccurred = false;
        bool hasSteelHit = false;
        bool hasBrickHit = false;

        for (int r = rMin; r <= rMax; r++)
        {
            for (int c = cMin; c <= cMax; c++)
            {
                var cell = _grid[r, c];

                if (cell == SubTileType.Eagle)
                {
                    IsEagleDestroyed = true;
                    hitEagle = true;
                    _grid[24, 12] = SubTileType.DestroyedEagle;
                    _grid[24, 13] = SubTileType.DestroyedEagle;
                    _grid[25, 12] = SubTileType.DestroyedEagle;
                    _grid[25, 13] = SubTileType.DestroyedEagle;
                    soundEffect = AudioSoundEffect.EagleHit;
                    IsDirty = true;
                    return true;
                }

                if (cell == SubTileType.Steel)
                {
                    hitOccurred = true;
                    if (bullet.CanBreakSteel)
                    {
                        _grid[r, c] = SubTileType.Empty;
                        hasBrickHit = true;
                    }
                    else
                    {
                        hasSteelHit = true;
                    }
                }
                else if (cell == SubTileType.Brick)
                {
                    hitOccurred = true;
                    hasBrickHit = true;
                    _grid[r, c] = SubTileType.Empty;
                }
            }
        }

        if (hitOccurred)
        {
            if (hasSteelHit && !hasBrickHit)
            {
                soundEffect = AudioSoundEffect.HitSteel;
            }
            else
            {
                soundEffect = AudioSoundEffect.HitBrick;
            }
            IsDirty = true;
            return true;
        }

        return false;
    }

    public byte[] GetSubTileBytes()
    {
        int idx = 0;
        for (int r = 0; r < IDestructibleMap.GridDimension; r++)
        {
            for (int c = 0; c < IDestructibleMap.GridDimension; c++)
            {
                _flatBytes[idx++] = (byte)_grid[r, c];
            }
        }
        return _flatBytes;
    }
}
