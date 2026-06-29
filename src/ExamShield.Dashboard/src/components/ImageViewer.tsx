import { useState } from 'react'

const STEP = 0.25
const MIN_SCALE = 0.5
const MAX_SCALE = 4

interface Props {
  src: string
  alt: string
}

export default function ImageViewer({ src, alt }: Props) {
  const [scale, setScale] = useState(1)
  const [rotation, setRotation] = useState(0)
  const [brightness, setBrightness] = useState(100)
  const [contrast, setContrast] = useState(100)

  const zoomIn  = () => setScale(s => Math.min(MAX_SCALE, +(s + STEP).toFixed(2)))
  const zoomOut = () => setScale(s => Math.max(MIN_SCALE, +(s - STEP).toFixed(2)))
  const rotate  = () => setRotation(r => (r + 90) % 360)
  const reset   = () => { setScale(1); setRotation(0); setBrightness(100); setContrast(100) }

  const transform = `scale(${scale}) rotate(${rotation}deg)`
  const filter    = `brightness(${brightness}%) contrast(${contrast}%)`

  const btnCls = 'px-2.5 py-1.5 rounded-lg text-sm font-mono text-foreground transition-colors hover:bg-muted/60 active:scale-95'
  const divider = <div className="w-px h-5 bg-border mx-1" />

  return (
    <div className="flex flex-col gap-3 select-none">
      {/* Controls */}
      <div className="glass flex flex-wrap items-center gap-2 p-3 rounded-2xl">
        <button title="Zoom in"   onClick={zoomIn}  className={btnCls}>+</button>
        <button title="Zoom out"  onClick={zoomOut} className={btnCls}>−</button>
        <span className="text-muted-foreground text-xs w-10 text-center tabular-nums">
          {Math.round(scale * 100)}%
        </span>

        {divider}

        <button title="Rotate 90°" onClick={rotate} className={btnCls}>↻</button>

        {divider}

        <label htmlFor="brightness" className="text-muted-foreground text-xs">Brightness</label>
        <input
          id="brightness"
          type="range"
          min={50} max={200}
          value={brightness}
          onChange={e => setBrightness(Number(e.target.value))}
          className="w-20 accent-primary"
          aria-label="Brightness"
        />
        <span className="text-muted-foreground text-xs w-8 tabular-nums">{brightness}%</span>

        <label htmlFor="contrast" className="text-muted-foreground text-xs">Contrast</label>
        <input
          id="contrast"
          type="range"
          min={50} max={200}
          value={contrast}
          onChange={e => setContrast(Number(e.target.value))}
          className="w-20 accent-primary"
          aria-label="Contrast"
        />
        <span className="text-muted-foreground text-xs w-8 tabular-nums">{contrast}%</span>

        {divider}

        <button title="Reset" onClick={reset} className="px-2.5 py-1.5 rounded-lg text-xs text-muted-foreground hover:text-foreground transition-colors">
          Reset
        </button>
      </div>

      {/* Image canvas */}
      <div className="glass overflow-auto rounded-2xl min-h-64 flex items-center justify-center p-4"
        style={{ background: 'rgba(0,0,0,0.3)' }}>
        <img
          src={src}
          alt={alt}
          draggable={false}
          style={{ transform, filter, transition: 'transform 0.15s ease, filter 0.15s ease', maxWidth: 'none' }}
          className="block"
        />
      </div>
    </div>
  )
}
