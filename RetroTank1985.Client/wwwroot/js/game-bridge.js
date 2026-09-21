/**
 * game-bridge.js — Hybrid Bridge: C# Game Brain + JS Fast Canvas & Audio Muscle
 * Handles input capture (split 2-player keyboard & touch), 60 FPS requestAnimationFrame loop,
 * and delegating rendering & audio.
 */

window.GameBridge = (function () {
  let canvas = null;
  let ctx = null;
  let dotNetRef = null;
  let animFrameId = null;
  let isRunning = false;

  // Input states
  const keys = {
    // Player 1 (WASD + Space/J)
    up: false,
    down: false,
    left: false,
    right: false,
    fire: false,

    // Player 2 (Arrows + Enter/K/L/Numpad0)
    p2Up: false,
    p2Down: false,
    p2Left: false,
    p2Right: false,
    p2Fire: false,

    // System
    pause: false,
    pausePulse: false
  };

  // FPS calculation
  let frameCount = 0;
  let lastFpsCalc = 0;
  let currentFps = 60;

  // Key listeners
  function onKeyDown(e) {
    if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space'].includes(e.code)) {
      e.preventDefault();
    }
    switch (e.code) {
      // Movement (WASD or Arrow Keys)
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

      // Fire (Space, J, Enter, Numpad0, K, L)
      case 'Space':
      case 'KeyJ':
      case 'Enter':
      case 'Numpad0':
      case 'KeyK':
      case 'KeyL':
        keys.fire = true;
        // Also keep p2Fire for 2P mode compatibility
        keys.p2Fire = true;
        break;

      // Global
      case 'KeyP':
      case 'Escape':
        keys.pause = true;
        break;
      case 'KeyR':
        if (dotNetRef) {
          dotNetRef.invokeMethodAsync('RestartCurrentStage');
        }
        break;
      case 'KeyC':
        // Trigger Emote Wheel Toggle event
        window.dispatchEvent(new CustomEvent('retrotank:toggle-emote'));
        break;
      case 'KeyF':
        if (!e.repeat && !e.ctrlKey && !e.altKey && !e.metaKey) {
          const arcadeElem = document.getElementById('arcadeBezelContainer') || canvas;
          if (arcadeElem) {
            window.GameBridge.toggleFullscreen(arcadeElem.id);
          }
        }
        break;
    }
  }

  function onKeyUp(e) {
    switch (e.code) {
      // Movement (WASD or Arrow Keys)
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

      // Fire (Space, J, Enter, Numpad0, K, L)
      case 'Space':
      case 'KeyJ':
      case 'Enter':
      case 'Numpad0':
      case 'KeyK':
      case 'KeyL':
        keys.fire = false;
        keys.p2Fire = false;
        break;

      // Global
      case 'KeyP':
      case 'Escape':
        keys.pause = false;
        break;
    }
  }

  // Audio Dispatching to nesSynth
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
          if (window.nesSynth.playLifeUp) {
            window.nesSynth.playLifeUp();
          } else if (window.nesSynth.playLife) {
            window.nesSynth.playLife();
          }
          break;
        case 12: // HitArmor
          window.nesSynth.playHitArmor();
          break;
        case 13: // BonusAppear
          window.nesSynth.playBonusAppear();
          break;
        case 14: // TallyTick
          window.nesSynth.playTallyTick();
          break;
        case 15: // TallyDone
          window.nesSynth.playTallyDone();
          break;
        case 16: // StageClear
          window.nesSynth.playStageClearBGM();
          break;
        case 17: // GameOver
          window.nesSynth.playGameOverBGM();
          break;
        case 18: // RadioChirp
          if (window.nesSynth.playRadioChirp) {
            window.nesSynth.playRadioChirp();
          }
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
          keys.p2Up,
          keys.p2Down,
          keys.p2Left,
          keys.p2Right,
          keys.p2Fire,
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
      } finally {
        // Reset single-pulse pause if it was triggered via touch/click
        if (keys.pausePulse) {
          keys.pause = false;
          keys.pausePulse = false;
        }
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
      // Handled in C#
    },

    togglePause() {
      keys.pause = true;
      keys.pausePulse = true;
    },

    setVirtualInput(control, isPressed) {
      if (control === 'up') keys.up = isPressed;
      if (control === 'down') keys.down = isPressed;
      if (control === 'left') keys.left = isPressed;
      if (control === 'right') keys.right = isPressed;
      if (control === 'fire') keys.fire = isPressed;

      if (control === 'p2Up') keys.p2Up = isPressed;
      if (control === 'p2Down') keys.p2Down = isPressed;
      if (control === 'p2Left') keys.p2Left = isPressed;
      if (control === 'p2Right') keys.p2Right = isPressed;
      if (control === 'p2Fire') keys.p2Fire = isPressed;

      if (control === 'pause') {
        if (isPressed) {
          keys.pause = true;
          keys.pausePulse = true;
        }
      }
    },

    toggleFullscreen(elementId) {
      const elem = (elementId ? document.getElementById(elementId) : null) || canvas || document.documentElement;
      
      const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) || 
                    (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

      if (isIOS) {
        // iOS Safari on iPhone doesn't support requestFullscreen on div/canvas
        elem.classList.toggle('is-pseudo-fullscreen');
        window.scrollTo(0, 0);
        return;
      }

      // Standard W3C Fullscreen API (Android Chrome, iPadOS with full API, Desktop)
      if (!document.fullscreenElement && !document.webkitFullscreenElement) {
        if (elem.requestFullscreen) {
          elem.requestFullscreen().catch(err => {
            console.warn('Standard fullscreen failed, falling back to pseudo-fullscreen:', err);
            elem.classList.toggle('is-pseudo-fullscreen');
          });
        } else if (elem.webkitRequestFullscreen) {
          elem.webkitRequestFullscreen();
        } else {
          elem.classList.toggle('is-pseudo-fullscreen');
        }
      } else {
        if (document.exitFullscreen) {
          document.exitFullscreen().catch(err => console.warn('Exit fullscreen failed:', err));
        } else if (document.webkitExitFullscreen) {
          document.webkitExitFullscreen();
        }
        elem.classList.remove('is-pseudo-fullscreen');
      }
    },

    registerPlayComponent(playDotNetRef) {
      window.__playRef = playDotNetRef;
      if (!window.__playEmoteHandlerInstalled) {
        window.__playEmoteHandlerInstalled = true;
        window.addEventListener('retrotank:toggle-emote', () => {
          if (window.__playRef) {
            window.__playRef.invokeMethodAsync('ToggleEmoteWheel');
          }
        });
      }
    },

    unregisterPlayComponent() {
      window.__playRef = null;
    }
  };
})();
