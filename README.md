# Codeplay — Endless Runner with Custom Joystick

A video game built for the [Codeplay](https://codeplay.hr) hackathon, where students design and build their own joystick controllers from scratch, then play a game with them.

## About

Codeplay is an endless runner combat game built in Unity. The player runs forward automatically, jumping over obstacles, sliding under barriers, shooting enemies, and activating shields — all controlled through a custom Arduino-based joystick that students assemble themselves.

The game progressively unlocks controller features across three phases, letting players experience each part of the hardware they built:

1. **Buttons** — Jump, slide, shoot, and shield using four physical buttons
2. **Analog Stick** — Aim across three lanes to target enemies and obstacles
3. **Tilt** — Use the accelerometer to switch lanes and collect coins

## How It Works

The custom joystick connects over serial (USB) and sends a 12-byte binary packet containing button states, analog stick position, and accelerometer tilt data. The game reads this raw input on a background thread and translates it into game actions.

## Features

- Infinite scrolling ground with object pooling
- Two obstacle types (jump over / slide under)
- Ground and air enemies with distinct behaviors
- Player shooting with lane-targeted aiming via analog stick
- Shield mechanic to block damage and projectiles
- Tilt-based lane switching
- Coins and health pickups
- Score multiplier system (distance + kills + coins)
- Difficulty scaling over time
- Full game state machine (menu, playing, game over)

## Tech

- **Engine:** Unity (C#)
- **Input:** Arduino over serial
- **Art:** Unity Asset Store, custom shaders

## Learn More

Visit [codeplay.hr](https://codeplay.hr) to learn more about the hackathon.
