/*
* Decompiled with Jsxer
* Version: 1.6.1
* JSXBIN 2.0
*/

function main() {
var oldunit = app.preferences.rulerUnits;
app.preferences.rulerUnits = Units.PIXELS;
var doc = activeDocument;
var al = doc.activeLayer;
if (al.textItem.kind != TextType.POINTTEXT) { 
al.textItem.kind = TextType.POINTTEXT;
}
var bnd = al.bounds;
var forh = ((bnd[2] - bnd[0]) / 2) + bnd[0];
var forv = ((bnd[3] - bnd[1]) / 2) + bnd[1];
al.visible = false;
select(forh, forv);
al.visible = true;
var sbnd = doc.selection.bounds;
var amunt = ((sbnd[2] - sbnd[0]) / 10) + ((sbnd[3] - sbnd[1]) / 15);
cleanTextLayerSpaces();
doc.selection.contract(Number(amunt));
doc.selection.expand(Number(amunt));
align();
doc.selection.deselect();
al.textItem.kind = TextType.PARAGRAPHTEXT;
app.preferences.rulerUnits = oldunit;
}
function select(h, v) {
var desc1 = new ActionDescriptor();
var ref1 = new ActionReference();
ref1.putProperty(charIDToTypeID("Chnl"), stringIDToTypeID("selection"));
desc1.putReference(charIDToTypeID("null"), ref1);
var desc2 = new ActionDescriptor();
desc2.putUnitDouble(charIDToTypeID("Hrzn"), charIDToTypeID("#Pxl"), h);
desc2.putUnitDouble(charIDToTypeID("Vrtc"), charIDToTypeID("#Pxl"), v);
desc1.putObject(charIDToTypeID("T   "), charIDToTypeID("Pnt "), desc2);
desc1.putInteger(charIDToTypeID("Tlrn"), 64);
desc1.putBoolean(charIDToTypeID("Mrgd"), true);
desc1.putBoolean(charIDToTypeID("AntA"), true);
executeAction(charIDToTypeID("setd"), desc1, DialogModes.NO);
}
function align() {
var desc1 = new ActionDescriptor();
var ref1 = new ActionReference();
ref1.putEnumerated(charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt"));
desc1.putReference(charIDToTypeID("null"), ref1);
desc1.putEnumerated(charIDToTypeID("Usng"), charIDToTypeID("ADSt"), stringIDToTypeID("ADSCentersH"));
desc1.putBoolean(stringIDToTypeID("alignToCanvas"), false);
executeAction(charIDToTypeID("Algn"), desc1, DialogModes.NO);
var desc1 = new ActionDescriptor();
var ref1 = new ActionReference();
ref1.putEnumerated(charIDToTypeID("Lyr "), charIDToTypeID("Ordn"), charIDToTypeID("Trgt"));
desc1.putReference(charIDToTypeID("null"), ref1);
desc1.putEnumerated(charIDToTypeID("Usng"), charIDToTypeID("ADSt"), stringIDToTypeID("ADSCentersV"));
desc1.putBoolean(stringIDToTypeID("alignToCanvas"), false);
executeAction(charIDToTypeID("Algn"), desc1, DialogModes.NO);
}
function cleanTextLayerSpaces() {
var activeLayer = app.activeDocument.activeLayer;
if (activeLayer.kind === LayerKind.TEXT) {
var textItem = activeLayer.textItem;
if (textItem.kind === TextType.POINTTEXT) {
var newTextLines = textItem.contents.split('\r');
var cleanedLines = [];         
for (var i = 0; i < newTextLines.length; i++) {
var line = newTextLines[i];
var cleanedLine = line.replace(/^\s+|\s+$/g, ''); // Remove leading and trailing spaces
cleanedLines.push(cleanedLine);
}
textItem.contents = cleanedLines.join('\r');
}
}
}
if (app.documents.length > 0) { 
app.activeDocument.suspendHistory("Align Layer to Bubble By Abdul", " main()");
}