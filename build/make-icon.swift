// ConvertiWall for Mac — app icon.
//
// Mootilda's ConvertiWall had no icon, so this one is new: a section of brick
// wall with a blue "convert" badge (two circling arrows) — a wall being
// changed into another kind of wall.
//
// Usage: swift build/make-icon.swift <out.png>   (1024x1024 PNG)
// build/make-icon.sh turns it into build/AppIcon.icns.

import AppKit

let size = 1024.0
let out = CommandLine.arguments[1]
let cs = CGColorSpace(name: CGColorSpace.sRGB)!
let ctx = CGContext(data: nil, width: Int(size), height: Int(size), bitsPerComponent: 8, bytesPerRow: 0,
                    space: cs, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!

func rgb(_ r: Double, _ g: Double, _ b: Double, _ a: Double = 1) -> CGColor { CGColor(srgbRed: r / 255, green: g / 255, blue: b / 255, alpha: a) }

// macOS icon grid: 824-point rounded square centred on the 1024 canvas.
let tile = CGRect(x: 100, y: 100, width: 824, height: 824)
let tilePath = CGPath(roundedRect: tile, cornerWidth: 185, cornerHeight: 185, transform: nil)

// Soft drop shadow under the tile.
ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: -12), blur: 28, color: rgb(0, 0, 0, 0.30))
ctx.addPath(tilePath); ctx.setFillColor(rgb(255, 255, 255)); ctx.fillPath()
ctx.restoreGState()

// Tile: pale sky gradient.
ctx.saveGState()
ctx.addPath(tilePath); ctx.clip()
let sky = CGGradient(colorsSpace: cs, colors: [rgb(214, 234, 250), rgb(246, 250, 255)] as CFArray, locations: [0, 1])!
ctx.drawLinearGradient(sky, start: CGPoint(x: 0, y: 100), end: CGPoint(x: 0, y: 924), options: [])

// Grass strip along the bottom.
ctx.setFillColor(rgb(120, 180, 90))
ctx.fill(CGRect(x: 100, y: 100, width: 824, height: 150))

// Brick wall.
let wall = CGRect(x: 190, y: 220, width: 644, height: 470)
let mortar = rgb(222, 212, 196)
ctx.setFillColor(mortar); ctx.fill(wall)
let rows = 7, brickW = 184.0, gap = 14.0
let rowH = (wall.height - gap) / Double(rows)
ctx.saveGState()
ctx.clip(to: wall)
for r in 0..<rows {
    let y = wall.minY + gap + Double(r) * rowH
    let offset = r % 2 == 0 ? 0.0 : -brickW / 2
    var x = wall.minX + gap + offset
    var i = 0
    while x < wall.maxX {
        // Slightly varied reds so the wall reads as brick, not stripes.
        let shade = [0.0, 10.0, -8.0, 5.0][(r + i) % 4]
        ctx.setFillColor(rgb(186 + shade, 72 + shade / 2, 52))
        ctx.fill(CGRect(x: x, y: y, width: brickW - gap, height: rowH - gap))
        x += brickW; i += 1
    }
}
ctx.restoreGState()
// Wall cap.
ctx.setFillColor(rgb(150, 150, 150))
ctx.fill(CGRect(x: wall.minX - 18, y: wall.maxY, width: wall.width + 36, height: 34))
ctx.restoreGState()

// Convert badge: blue disc, white ring, two circling arrows.
let c = CGPoint(x: 700, y: 700), R = 190.0
ctx.saveGState()
ctx.setShadow(offset: CGSize(width: 0, height: -8), blur: 20, color: rgb(0, 0, 0, 0.35))
ctx.setFillColor(rgb(255, 255, 255)); ctx.fillEllipse(in: CGRect(x: c.x - R - 14, y: c.y - R - 14, width: 2 * R + 28, height: 2 * R + 28))
ctx.restoreGState()
ctx.setFillColor(rgb(36, 112, 214)); ctx.fillEllipse(in: CGRect(x: c.x - R, y: c.y - R, width: 2 * R, height: 2 * R))

let ar = 112.0, lw = 34.0
ctx.setStrokeColor(rgb(255, 255, 255)); ctx.setFillColor(rgb(255, 255, 255))
ctx.setLineWidth(lw); ctx.setLineCap(.round)
for start in [Double.pi * 0.15, Double.pi * 1.15] {
    let end = start + Double.pi * 0.72
    ctx.addArc(center: c, radius: ar, startAngle: start, endAngle: end, clockwise: false)
    ctx.strokePath()
    // Arrowhead at the end of the arc, pointing along the direction of travel.
    let tip = CGPoint(x: c.x + ar * cos(end), y: c.y + ar * sin(end))
    let dir = CGPoint(x: -sin(end), y: cos(end))          // tangent (counter-clockwise)
    let nrm = CGPoint(x: cos(end), y: sin(end))           // outward normal
    let head = 62.0, half = 46.0
    ctx.move(to: CGPoint(x: tip.x + dir.x * head, y: tip.y + dir.y * head))
    ctx.addLine(to: CGPoint(x: tip.x + nrm.x * half, y: tip.y + nrm.y * half))
    ctx.addLine(to: CGPoint(x: tip.x - nrm.x * half, y: tip.y - nrm.y * half))
    ctx.closePath(); ctx.fillPath()
}

let rep = NSBitmapImageRep(cgImage: ctx.makeImage()!)
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: out))
