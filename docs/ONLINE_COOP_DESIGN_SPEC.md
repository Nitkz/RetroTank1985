# 🌐 ONLINE 2-PLAYER CO-OP DESIGN SPECIFICATION: เรียลไทม์มัลติเพลเยอร์ออนไลน์ข้ามแพลตฟอร์ม
> **Phase 8 Feature Design Document**  
> **Project:** Battle City: RetroTank 1985 (NES Authenticity & Modern Polish)  
> **Author:** Antigravity AI & RetroTank Core Team  
> **Version:** 1.0 (Real-Time Peer-to-Peer & SignalR Multiplay Architecture)

---

## 1. 📖 บทนำและเป้าหมายการออกแบบ (Overview & Core Goals)

ระบบ **Online 2-Player Co-Op** เป็นฟีเจอร์หลักในการขยายประสบการณ์การเล่นจาก **Local 2P (เครื่องเดียวกัน/คีย์บอร์ดแยก)** สู่ **Online Multiplayer (เล่นด้วยกันผ่านอินเทอร์เน็ต)** โดยเปิดโอกาสให้ผู้เล่น 2 คนจากคนละอุปกรณ์ (PC Desktop, Tablet, Mobile Smartphone) สามารถสร้างห้อง ร่วมทีมปกป้องฐานทัพนกอินทรี และตะลุยด่าน 35 Stage ร่วมกันแบบเรียลไทม์ 60 FPS

```
+-------------------+                               +-------------------+
|  PLAYER 1 (HOST)  | <======== DataChannel =======> |  PLAYER 2 (GUEST) |
|   Yellow Tank 1P  |          (WebRTC P2P)         |   Green Tank 2P   |
| (Physics Authority|                               | (Client Prediction|
|  & Enemy Engine)  | <--- Audio / Chat / Inputs -- |   & Interpolation)|
+-------------------+                               +-------------------+
          ^                                                   ^
          |=========== Signaling / Room Matchmaking ==========|
                    (SignalR Hub / WebRTC STUN-TURN)
```

### 🎯 เป้าหมายหลัก (Design Goals)
1. **Zero-Latency Feel (< 50ms UX)**: สัมผัสการบังคับรถถังที่ลื่นไหลระดับ 60 FPS ด้วยระบบ **Client-Side Prediction** และ **Linear Entity Interpolation** สำหรับฝั่ง Guest (P2)
2. **Serverless & Dual-Mode Hosting**:
   - **Static WASM Deployment (Cloudflare Pages)**: ทำงานผ่าน **WebRTC Peer-to-Peer (DataChannels)** + Lightweight STUN/Signaling Service โดยไม่ต้องเช่า Cloud Server ราคาแพง
   - **Self-Hosted ASP.NET Core**: รองรับ **SignalR Binary WebSockets Hub** สำหรับโหมดรันบน Server เต็มรูปแบบ
3. **Frictionless Onboarding (เข้าเล่นง่าย ไม่ต้องล็อกอิน)**:
   - ระบบ **Room Code 6 ตัวอักษร** (เช่น `TANK85`, `EAGLE1`)
   - **One-Click Invite URL** (เช่น `https://retrotank1985.pages.dev/coop?room=TANK85`)
   - **QR Code Generator** ในตัว ให้ผู้เล่นบนมือถือสแกนเข้าห้องเล่นกับผู้เล่นบน PC ได้ทันที
4. **Authentic Co-Op Mechanics (คงเสน่ห์ Famicom Battle City)**:
   - ระบบขอยืมชีวิต (Life Steal / Borrow Life) เมื่อผู้เล่นคนใดคนหนึ่งตายหมดตัว
   - ระบบยกเลิกกระสุนพันธมิตร (Friendly Fire Spark Clink) หรือโหมดสตันชั่วคราว
   - สรุปคะแนนท้ายด่านแบบ 2 คอลัมน์แยกรายบุคคล พร้อมระบบเหรียญ MVP และคำนวณชัยชนะ

---

## 2. 🏗️ สถาปัตยกรรมเน็ตเวิร์ก (Network Architecture & Topology)

```
                       +-----------------------------+
                       |    SIGNALING / LOBBY API    |
                       |  (SignalR / WebRTC Broker)  |
                       +--------------+--------------+
                                      |
                      Exchange SDP Offer / ICE Candidates
                                      |
               +----------------------+----------------------+
               |                                             |
               v                                             v
     +-------------------+                         +-------------------+
     |  PLAYER 1 (HOST)  |   Direct UDP / WebRTC   |  PLAYER 2 (GUEST) |
     |  .NET 9 WASM/C#   | <=====================> |  .NET 9 WASM/C#   |
     |  - Full Sim Core  |   Low-Latency Channel   |  - Local Input    |
     |  - Enemy AI & Map |                         |  - Snapshot Interp|
     |  - State Broadcast|                         |  - Remote Render  |
     +-------------------+                         +-------------------+
```

### 2.1 รูปแบบการเชื่อมต่อ (Dual Connection Modes)

| โหมด | เทคโนโลยี | การใช้งานหลัก | ข้อดี |
| :--- | :--- | :--- | :--- |
| **Mode 1: WebRTC P2P (Default)** | WebRTC DataChannels (UDP) + STUN | Static Cloudflare Pages / Standalone WASM | Latency ต่ำที่สุด (P2P ตรง), ประหยัดทรัพยากรเซิร์ฟเวอร์, รองรับผู้เล่นทั่วโลก |
| **Mode 2: SignalR WebSocket** | ASP.NET Core SignalR + MessagePack | Local Dev & Dedicated Docker/Server Host | เสถียรสูงในเครือข่ายองค์กร/NAT ซับซ้อน, จัดการ Spectator ได้ง่าย |

### 2.2 โมเดลการคำนวณเกม (Host-Authoritative with Client Prediction)

1. **Host Authority (Player 1 - Yellow Tank)**:
   - รัน Game Engine C# ฉบับเต็ม (Enemy Spawner, AI Pathfinding, Collision Physics, Destructible Map, Power-Up RNG, Phoenix Eagle Base State)
   - รับ Input Stream ของ Player 2 มาประมวลผลใน Fixed Timestep 60Hz
   - บรอดแคสต์ Snapshot สภาพแวดล้อม (`CoopSyncSnapshotDto`) ไปยัง Guest ด้วยความถี่ 30–60Hz (ขึ้นอยู่กับ Bandwidth)
2. **Guest Prediction & Interpolation (Player 2 - Green Tank)**:
   - คำนวณการเคลื่อนที่ของ P2 Tank ทันทีในเครื่องตัวเอง (Local Prediction) เพื่อให้การเลี้ยวและกดยิงตอบสนอง 0ms ไม่มีดีเลย์
   - ปรับตำแหน่งศัตรูและกระสุนของ Host ด้วยเทคนิค **Hermite Entity Interpolation** (บัฟเฟอร์ 1-2 เฟรม) เพื่อภาพที่นุ่มนวล
   - ระบบ **Desync Reconciliation**: หากตำแหน่งของ P2 ในฝั่ง Host คลาดเคลื่อนเกิน 4px เครื่อง Guest จะค่อยๆ ปรับสมดุล (Soft Snap) กลับมาตำแหน่งจริงโดยไม่กระตุก

---

## 3. 📦 โครงสร้างข้อมูลและเพย์โหลด (Data Contract & Delta Sync)

เพื่อลดขนาดข้อมูลที่ส่งผ่านเน็ตเวิร์กให้มีขนาดเล็กมาก (< 2 KB/s) เราใช้โครงสร้าง Compact DTO และ Delta Compression:

### 3.1 โครงสร้างข้อมูล P2 Input Stream (Client &rarr; Host, 60Hz)

```csharp
public struct PlayerInputPacket
{
    public uint Sequence;       // เลขลำดับเฟรมอินพุต
    public byte Direction;      // 0=None, 1=Up, 2=Right, 3=Down, 4=Left
    public bool IsFiring;       // กดยิงกระสุน
    public bool BorrowLifeReq;  // กดขอยืมชีวิตเพื่อนร่วมทีม
    public byte EmoteId;        // รหัส Emote ส่งข้อความสั้น
    public ushort ClientTimestamp; // Timestamp สำหรับวัด RTT Ping
}
```

### 3.2 โครงสร้างข้อมูล Host State Snapshot (Host &rarr; Client, 30-60Hz)

```csharp
public class CoopSyncSnapshotDto
{
    public uint FrameIndex;             // หมายเลขเฟรมโลก (World Tick)
    public ushort AckP2Sequence;        // ลำดับอินพุต P2 ล่าสุดที่ Host ประมวลผลแล้ว
    
    // สถานะผู้เล่น (16 bytes)
    public TankSnapshotDto P1;
    public TankSnapshotDto P2;

    // สถานะรถถังศัตรู (Active สูงสุด 4-6 คัน)
    public EnemySnapshotDto[] Enemies;

    // กระสุนที่กำลังบินอยู่ (Active สูงสุด 16 นัด)
    public BulletSnapshotDto[] Bullets;

    // แผนที่ถูกทำลาย (ส่งเฉพาะเมื่อมีการยิงโดนกำแพงในเฟรมนั้น - Delta Event)
    public MapMutationDto[]? MapDelta;

    // ไอเทม Power-Up และสถานะฐานอินทรี
    public PowerUpSnapshotDto? ActivePowerUp;
    public bool IsEagleDestroyed;

    // เสียงและเอฟเฟกต์ระเบิดที่เกิดขึ้นในเฟรมนี้
    public byte[] SoundEvents;
    public ExplosionSnapshotDto[]? NewExplosions;
}
```

---

## 4. 🎮 ระบบล็อบบี้และการจับคู่ห้อง (Lobby & Matchmaking Flow)

```
 [MENU: 2-PLAYER ONLINE]
          |
          +---> [1. CREATE ROOM (HOST)] ---> แสดง Room Code: "TANK85" + ลิงก์เชิญ + QR Code
          |                                       |
          |                                       v รอ Player 2 เข้าร่วม
          |                                  [LOBBY READY ROOM]
          |                                  - P1 (Host) 🟡 READY
          |                                  - P2 (Guest) 🟢 READY
          |                                  - Stage: [STAGE 01 ▼]
          |                                  - Ping: 🟢 18ms
          |                                       |
          |                                  [START MISSION]
          |
          +---> [2. JOIN ROOM (GUEST)] ----> ป้อน Room Code: [ _ _ _ _ _ _ ] หรือกดจาก Invite Link
```

### 4.1 หน้าจอ Lobby Room UI (`/coop`)
- **Host Room Card**: แสดงข้อมูลผู้เล่น 1 (สีเหลือง), สิทธิ์ในการเลือก Stage 01–35, สิทธิ์ในการเลือกระดับความยาก (Original 1985 / Tactical Armor / Kids Safe)
- **Guest Room Card**: แสดงข้อมูลผู้เล่น 2 (สีเขียว), ปุ่มกดยืนยันความพร้อม `[ READY ]`
- **Ping / Connection Quality Indicator**:
  - 🟢 **Excellent**: Ping $< 50\text{ ms}$ (Jitter $< 5\text{ ms}$)
  - 🟡 **Good**: Ping $50 - 120\text{ ms}$
  - 🔴 **Poor**: Ping $> 120\text{ ms}$ (แสดงไอคอนแจ้งเตือน Network Lag)
- **Instant Invite Tools**:
  - ปุ่ม `[ 📋 COPY INVITE LINK ]` คัดลอก URL เข้า Clipboard
  - ปุ่ม `[ 📱 SHOW QR CODE ]` เปิดโมดอล QR ให้เพื่อนสแกนด้วยกล้องมือถือ

---

## 5. 🤝 ฟีเจอร์การเล่นร่วมกันแบบออนไลน์ (Co-Op Gameplay Mechanics)

### 5.1 ระบบกู้ชีพและยืมชีวิต (Life Steal / Borrow Life System)
- ถอดแบบความสนุกจาก Famicom Battle City ของแท้:
  - หาก **Player 1 หรือ Player 2 ตายจนชีวิตเหลือ 0** ขณะที่อีกคนยังมีชีวิต $\ge 2$ ตัว
  - ผู้เล่นที่ตายสามารถกดปุ่ม **[FIRE]** ในจังหวะ Respawn เพื่อ **"ขอยืมชีวิตเพื่อนร่วมทีมมา 1 ชีวิต"**
  - มีเสียง Jingle พิเศษ `1-UP Jingle (Inverted)` แจ้งเตือนทั้งสองฝ่าย พร้อมเอฟเฟกต์ดาวเกิดใหม่

### 5.2 ระบบ Friendly Fire และ Anti-Griefing
- **Default Mode (Classic Co-Op)**: กระสุนของผู้เล่นคนหนึ่งยิงโดนรถถังเพื่อนร่วมทีม จะเกิดประกายไฟ `Spark Clink` กระสุนสลายตัวโดยไม่ลดชีวิตเพื่อน แต่จะทำให้รถถังเพื่อนหยุดชะงัก (Stun Lock) ชั่วคราว 0.5 วินาที
- **Safe Mode (Kids & Family)**: ปิดระบบ Stun Lock อย่างสมบูรณ์ กระสุนทะลุผ่านเพื่อนได้ ช่วยให้เล่นด้วยกันได้ง่ายขึ้น
- **Anti-Troll Base Protection**: ป้องกันไม่ให้กระสุนของผู้เล่นทำลายฐานนกอินทรีของฝั่งตัวเอง

### 5.3 วงล้อสื่อสารด่วนยุค 8-บิต (Retro 8-Bit Emote Wheel)
- กดปุ่ม **`C`** (บนคีย์บอร์ด) หรือแตะ **ไอคอน Emote** (บนจอมือถือ) เพื่อเปิดวงล้อคำสั่งด่วน:
  1. 🛡️ **"DEFEND HQ!"** (กลับไปกันฐาน!)
  2. ⭐ **"TAKE STAR!"** (ให้นายเก็บดาว!)
  3. 🚀 **"ATTACK FLANK!"** (บุกขนาบข้าง!)
  4. 💣 **"NUKE BOMB!"** (เก็บระเบิดเลย!)
  5. 🤝 **"NICE SHOT!"** (ยิงสวยมาก!)
  6. 😅 **"SORRY!"** (ขอโทษที!)
- แสดงเป็น Balloon ข้อความสไตล์พิกเซล 8-Bit ลอยอยู่เหนือรถถัง 2.5 วินาที พร้อมเสียงบี๊บวิทยุทหาร `RadioChirp SFX`

```
          +------------------+
          | 🛡️ DEFEND EAGLE! |
          +--------+---------+
                   |
                [ P1 ] 🟡
```

### 5.4 การจัดการเมื่อผู้เล่นหลุดการเชื่อมต่อ (Disconnection & Reconnection)
- หากผู้เล่นคนใดคนหนึ่งอินเทอร์เน็ตขาดหาย เกมจะเข้าสู่สถานะ **"PAUSED: RECONNECTING TO P2..."** อัตโนมัติ (นับถอยหลัง 15 วินาที)
- หากเชื่อมต่อสำเร็จภายใน 15 วินาที เกมจะซิงก์ Full State ล่าสุดและดำเนินต่อทันทีโดยไม่ต้องเริ่มด่านใหม่
- หากเกิน 15 วินาที ผู้เล่นที่เป็น Host สามารถเลือกเล่นต่อคนเดียว (Single Player Conversion) หรือเปิดให้บอท AI ควบคุมแทน

---

## 6. 📊 สรุปคะแนนออนไลน์และเกียรติยศ (End-of-Stage Online Tally & MVP)

เมื่อเคลียร์ด่าน ระบบสรุปคะแนนจะแสดงตารางเปรียบเทียบผลงานของผู้เล่นทั้งสองอย่างสมบูรณ์แบบ:

```
========================================================================
                         STAGE 08 CLEAR!
========================================================================
       I-PLAYER (HOST)                      II-PLAYER (GUEST)
      🟡 NITIKORN (P1)                       🟢 GUEST_WARRIOR (P2)
------------------------------------------------------------------------
  SCORE: 18,400 PTS                    SCORE: 22,600 PTS
------------------------------------------------------------------------
   4 ⚪ BASIC TANK   400 PTS             6 ⚪ BASIC TANK   600 PTS
   2 🟡 FAST TANK    400 PTS             5 🟡 FAST TANK   1000 PTS
   3 🔴 POWER TANK   900 PTS             1 🔴 POWER TANK   300 PTS
   1 🟢 ARMOR TANK   400 PTS             3 🟢 ARMOR TANK  1200 PTS
------------------------------------------------------------------------
  TOTAL KILLS: 10 TANKS                TOTAL KILLS: 15 TANKS
  POWER-UPS:   2 ITEMS                 POWER-UPS:   4 ITEMS
------------------------------------------------------------------------
                     👑 MATCH MVP: II-PLAYER!
                     [ 🚀 CONTINUE TO STAGE 09 ]
========================================================================
```

---

## 7. 🛠️ แผนการพัฒนาระบบ Online Co-Op (Implementation Milestones)

| Milestone | รายละเอียดการพัฒนา | ไฟล์และโมดูลที่เกี่ยวข้อง | สถานะปัจจุบัน |
| :--- | :--- | :--- | :---: |
| **M1: Signaling & Matchmaking** | ระบบสร้างรหัสห้อง (Room Code), SignalR Hub / Fallback, WebRTC Signaling SDP Exchange | `Hubs/CoopLobbyHub.cs`, `Services/CoopLobbyClientService.cs`, `Contracts/ICoopLobbyContracts.cs` | 🟢 **เสร็จสมบูรณ์** |
| **M2: C# Engine Network Sync** | ออกแบบ Snapshot DTO, Engine State Serialization, Tick Snapshot Broadcast & Remote Guest Input | `Engine/Core/BattleCityEngine.cs`, `Services/GameEngineService.cs`, `Pages/Play.razor.cs` | 🟢 **เสร็จสมบูรณ์** |
| **M3: Client-Side Prediction** | เพิ่มการทำนายตำแหน่ง P2 ในเครื่อง Client, Entity Interpolation สำหรับลดอาการกระตุกของเครือข่าย | `Engine/Core/TankPhysics.cs`, `Engine/Network/ClientPredictor.cs` | 🟡 *รอดำเนินการ* |
| **M4: Co-Op UI & Room Lobby** | พัฒนาหน้า `/coop`, การ์ดจัดการห้อง, ระบบ Ready, In-App QR Code Share, Stage Selector | `Pages/Coop.razor`, `Components/Coop/CoopLobbyCard.razor`, `Components/Coop/QrCodeDialog.razor` | 🟢 **เสร็จสมบูรณ์** |
| **M5: In-Game Co-Op Features** | ระบบยืมชีวิต (Life Borrowing Logic พร้อมแล้ว), In-Game Emote Wheel, Disconnect Grace Period | `Components/Coop/EmoteWheel.razor`, `Engine/Core/BattleCityEngine.cs` | 🟡 *กำลังดำเนินการ* |
| **M6: Testing & Optimization** | ทดสอบข้ามเครือข่าย (Mobile 4G/5G vs Desktop WiFi), ปรับแต่งค่า Latency & Desync | Stress Testing & Packet Loss Simulation | ⚪ *รอดำเนินการ* |

---

*Last Updated: 2026-09-21 (Updated: M1, M2, M4 Completed & Synced) • RetroTank 1985 Multiplayer Core Team*
