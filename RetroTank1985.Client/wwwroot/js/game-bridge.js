/**
 * game-bridge.js — Hybrid Bridge: C# Game Brain + JS Fast Canvas & Audio Muscle
 * Handles input capture, 60 FPS requestAnimationFrame loop, and delegating rendering & audio.
 */

window.GameBridge = (function () {
  let canvas = null;
  let ctx = null;
  let dotNetRef = null;
  let animFrameId = null;
  let isRunning = false;

  // Input states
  const keys = {
    up: false,
    down: false,
    left: false,
    right: false,
    fire: false,
    pause: false
  };

  // FPS calculation
  let lastLoopTime = 0;
  let frameCount = 0;
  let lastFpsCalc = 0;
  let currentFps = 60;

  // Key listeners
  function onKeyDown(e) {
    if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space'].includes(e.code)) {
      e.preventDefault();
    }
    switch (e.code) {
      case 'KeyW':
      case 'ArrowUp':
        keys.up = true;
        break;
      case 'KeyS':
      case 'ArrowDown':
        keys.down = true;
        break;
      case 'KeyA':
      case 'ArrowLeft':
        keys.left = true;
        break;
      case 'KeyD':
      case 'ArrowRight':
        keys.right = true;
        break;
      case 'Space':
      case 'KeyJ':
        keys.fire = true;
        break;
      case 'KeyP':
      case 'Escape':
        keys.pause = true;
        break;
    }
  }

  function onKeyUp(e) {
    switch (e.code) {
      case 'KeyW':
      case 'ArrowUp':
        keys.up = false;
        break;
      case 'KeyS':
      case 'ArrowDown':
        keys.down = false;
        break;
      case 'KeyA':
      case 'ArrowLeft':
        keys.left = false;
        break;
      case 'KeyD':
      case 'ArrowRight':
        keys.right = false;
        break;
      case 'Space':
      case 'KeyJ':
        keys.fire = false;
        break;
      case 'KeyP':
      case 'Escape':
        keys.pause = false;
        break;
    }
  }

  // Audio Dispatching to nesSynth
  // 1=Shot, 2=HitBrick, 3=HitSteel, 4=Explosion, 5=EagleHit, 6=Pause, 7=IntroBgm, 8=EngineStart, 9=EngineStop, 10=Bonus, 11=Life
  function dispatchAudio(audioQueue) {
    if (!audioQueue || !window.nesSynth) return;

    for (let i = 0; i < audioQueue.length; i++) {
      const sfx = audioQueue[i];
      switch (sfx) {
        case 1: // Shot
          window.nesSynth.playShot();
          break;
        case 2: // HitBrick
          window.nesSynth.playHitBrick();
          break;
        case 3: // HitSteel
          window.nesSynth.playHitSteel();
          break;
        case 4: // Explosion
          window.nesSynth.playExplosion();
          break;
        case 5: // EagleHit
          window.nesSynth.playEagleHit();
          break;
        case 6: // Pause
          window.nesSynth.playPause();
          break;
        case 7: // IntroBgm
          window.nesSynth.playIntroBGM();
          break;
        case 8: // EngineStart
          window.nesSynth.startEngine(true);
          break;
        case 9: // EngineStop
          window.nesSynth.stopEngine();
          break;
        case 10: // Bonus
          window.nesSynth.playBonus();
          break;
        case 11: // Life
          window.nesSynth.playLife();
          break;
      }
    }
  }

  // Main 60 FPS RequestAnimationFrame Loop
  async function loop(timestamp) {
    if (!isRunning) return;

    // FPS Meter
    frameCount++;
    if (timestamp - lastFpsCalc >= 500) {
      currentFps = Math.round((frameCount * 1000) / (timestamp - lastFpsCalc));
      frameCount = 0;
      lastFpsCalc = timestamp;
    }

    if (dotNetRef) {
      try {
        const frameData = await dotNetRef.invokeMethodAsync(
          'OnEngineTick',
          timestamp,
          keys.up,
          keys.down,
          keys.left,
          keys.right,
          keys.fire,
          keys.pause,
          currentFps
        );

        if (frameData) {
          // 1. Dispatch Web Audio
          if (frameData.audioQueue && frameData.audioQueue.length > 0) {
            dispatchAudio(frameData.audioQueue);
          }

          // 2. Render Fast Canvas Frame
          if (window.StageRenderer && canvas) {
            window.StageRenderer.renderGameFrame(canvas, frameData);
          }
        }
      } catch (err) {
        console.error('[GameBridge] Tick error:', err);
      }
    }

    animFrameId = requestAnimationFrame(loop);
  }

  return {
    async init(canvasId, dotNetReference, stageGrid, stageNumber) {
      canvas = document.getElementById(canvasId);
      if (!canvas) {
        console.warn(`[GameBridge] Canvas #${canvasId} not found`);
        return false;
      }

      ctx = canvas.getContext('2d', { alpha: false });
      ctx.imageSmoothingEnabled = false;
      dotNetRef = dotNetReference;

      if (window.StageRenderer) {
        await window.StageRenderer.init();
        if (stageGrid) {
          await window.StageRenderer.prepareStageBackground(stageGrid, stageNumber);
        }
      }

      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
      window.addEventListener('keydown', onKeyDown);
      window.addEventListener('keyup', onKeyUp);

      return true;
    },

    start() {
      if (isRunning) return;
      isRunning = true;
      lastLoopTime = performance.now();
      lastFpsCalc = performance.now();
      frameCount = 0;
      animFrameId = requestAnimationFrame(loop);
    },

    stop() {
      isRunning = false;
      if (animFrameId) {
        cancelAnimationFrame(animFrameId);
        animFrameId = null;
      }
      if (window.nesSynth) {
        window.nesSynth.stopEngine();
        window.nesSynth.stopBGM();
      }
    },

    async setStage(stageGrid, stageNumber) {
      if (window.StageRenderer && stageGrid) {
        await window.StageRenderer.prepareStageBackground(stageGrid, stageNumber);
      }
    },

    resetPlayer() {
      // Nothing needed on JS side, handled in C#
    },

    togglePause() {
      // Pause is handled in C# OnEngineTick or SetPause
    },

    setVirtualInput(control, isPressed) {
      if (control === 'up') keys.up = isPressed;
      if (control === 'down') keys.down = isPressed;
      if (control === 'left') keys.left = isPressed;
      if (control === 'right') keys.right = isPressed;
      if (control === 'fire') keys.fire = isPressed;
      if (control === 'pause' && isPressed) keys.pause = true;
    }
  };
})();
