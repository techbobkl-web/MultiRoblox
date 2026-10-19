// Draws the app icon (two stacked windows on a green tile, same as the Windows tray icon)
// as a 1024x1024 PNG. Usage: swift make-icon.swift <out.png>
import AppKit

let size = 1024.0
let image = NSImage(size: NSSize(width: size, height: size))
image.lockFocus()
let tile = NSRect(x: 100, y: 100, width: 824, height: 824)
NSColor(srgbRed: 46 / 255, green: 160 / 255, blue: 67 / 255, alpha: 1).setFill()
NSBezierPath(roundedRect: tile, xRadius: 185, yRadius: 185).fill()
NSColor.white.set()
let back = NSBezierPath(rect: NSRect(x: 290, y: 430, width: 300, height: 300))
back.lineWidth = 56
back.stroke()
NSBezierPath(rect: NSRect(x: 434, y: 294, width: 300, height: 300)).fill()
image.unlockFocus()

let rep = NSBitmapImageRep(data: image.tiffRepresentation!)!
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: CommandLine.arguments[1]))
