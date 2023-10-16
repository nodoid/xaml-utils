using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace xib2xaml
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage : xib2xaml -m | -f (MAUI or Forms) <infile>.xib [Optional] <outfile>.xaml");
                Environment.Exit(-1);
            }

            var output = args.Length == 2 ? "" : args[2];
            var convert = new Converter(args[0], args[1], output);
            convert.ConvertFile();
        }
    }

    public static class DictUtils
    {
        public static string GetKeyValue(this NameValueCollection dict, string test, string defVal = "")
        {
            if (dict == null)
                return string.Empty;

            if (dict.AllKeys.Contains(test))
            {
                var rv = dict.GetValues(test).FirstOrDefault();
                return !string.IsNullOrEmpty(rv) ? rv : defVal;
            }
            else
                return defVal;
        }
    }

    class Converter
    {
        string infile, outfile;
        bool isMaui = false, hasScrollview = false;
        Dictionary<string, XElement> Outlets = new Dictionary<string, XElement>();
        UIObject ui;

        public Converter(string usem, string inf, string outf = "")
        {
            isMaui = usem.ToLower().Contains('m');
            infile = inf;
            outfile = !string.IsNullOrEmpty(outf) ? outf : infile;
            if (outfile == infile)
            {
                var ind = outfile.LastIndexOf('.');
                outfile = outfile.Substring(0, ind) + ".xaml";
            }
        }

        public void ConvertFile()
        {
            Console.WriteLine();
            Console.WriteLine("======================== NEW ===================");
            Console.WriteLine();

            CreateHeader(isMaui, outfile);

            var d = XDocument.Load(infile);
            var drootelem = d.Root.Elements();

            Console.WriteLine($"root element count = {drootelem.Count()}");
            Outlets.Clear();
            Generator(drootelem);

            CreateFooter();
        }

        void Generator(IEnumerable<XElement> elements, bool fromScrollview = false)
        {
            Console.WriteLine($"element count = {elements.Count()}, fromScrollview = {fromScrollview}");
            //Outlets.Clear();

            if (!hasScrollview)
                hasScrollview = fromScrollview;

            if (!fromScrollview)
            {
                Console.WriteLine("Not a scrollview");
                foreach (var de in elements.Elements())
                {
                    if (!string.IsNullOrEmpty(de.Attribute("id")?.Value))
                    {
                        int res = 0;
                        var val = int.TryParse(de.Attribute("id")?.Value, out res);
                        if (res == 0)
                        {
                            if (!de.Name.ToString().Contains("constrain"))
                                Console.WriteLine($"id = {de.Attribute("id").Value}, Name={de.Name}");
                            var des = de.Descendants();
                            foreach (var ds in des)
                            {
                                if (!string.IsNullOrEmpty(ds.Attribute("id")?.Value))
                                {
                                    if (ds.Name.ToString() != "constraint")
                                    {
                                        Outlets.Add(ds.Attribute("id").Value, ds);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
               Outlets =  GenerateScrollview(elements);
            }

            Console.WriteLine($"Outlets count = {Outlets.Count}");

            try
            {
                foreach (var node in Outlets)
                {
                    if (!fromScrollview)
                    {
                        ProcessNode(node.Value, node.Key);
                    }
                    else
                    {
                        if (node.Value.ToString().Contains("scrollView"))
                        {
                            Console.WriteLine("Create scrollview node");
                            var t = node.Value.ToString();
                            var start = t.IndexOf("<scrollView");
                            var end = t.IndexOf("</subviews>");

                            Console.WriteLine($"start = {start}, end = {end}, len = {t.Length}");

                            t = t.Substring(start, end + 11);
                            t += "</scrollView>";

                            ProcessNode(XElement.Parse(t),node.Key);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }


        Dictionary<string, XElement> GenerateScrollview(IEnumerable<XElement> elements)
        {
            var dict = new Dictionary<string, XElement>();
            var elem = elements.FirstOrDefault();

            Console.WriteLine($"Name = {elem.Name}");

            foreach (var db in elem.Elements())
            {
                if (db.Name == "subviews")
                {
                    var dt = db.Elements();
                    foreach (var dd in dt)
                    {
                        if (!string.IsNullOrEmpty(dd.Attribute("id")?.Value))
                        {
                            if (!dd.Name.ToString().Contains("constrain"))
                            {
                                Console.WriteLine($"id = {dd.Attribute("id").Value}, Name={dd.Name}");
                                dict.Add(dd.Attribute("id").Value, dd);
                            }
                        }
                    }
                }
            }

            return dict;
        }

        void ProcessNode(XElement node, string key)
        {
            if (node.Name.ToString().Contains("tabBar"))
                return;

            ui = new UIObject();
            var dict = ProcessMakeDict(node);

            ui.UIName =key;
            Console.WriteLine($"Node name = {ui.UIName}");

            try
            {
                ui.UIElement = node.Name.ToString();

                switch (ui.UIElement)
                {
                    case "button":
                        ui.UIElement = "Button";
                        break;
                    case "imageView":
                        ui.UIElement = "Image";
                        break;
                    case "label":
                        ui.UIElement = "Label";
                        break;
                    case "textField":
                        ui.UIElement = "Entry";
                        break;
                    case "textView":
                        ui.UIElement = "TextView";
                        break;
                    case "switch":
                        ui.UIElement = "Switch";
                        break;
                    case "scrollView":
                        ui.UIElement = "ScrollView";
                        break;
                    case "tableView":
                        ui.UIElement = "TableView";
                        break;
                    case "tableViewCell":
                        ui.UIElement = "TableViewCell";
                        break;
                    default:
                        ui.UIElement = string.Empty;
                        break;
                }

                if (string.IsNullOrEmpty(ui.UIElement))
                {
                    Console.WriteLine("Unknown element type");
                    return;
                }

                Console.WriteLine($"UI Element = {ui.UIElement}");

                if (ui.UIElement == "ScrollView")
                {
                    ui.UIXPos = dict.GetKeyValue("x", "0");
                    ui.UIYPos = dict.GetKeyValue("y", "0");
                    ui.UIWidth = dict.GetKeyValue("width", "0");
                    ui.UIHeight = dict.GetKeyValue("height", "0");
                    OutputUIElement(ui, ui.UIElement == "ScrollView");
                    ui = null;
                    Console.WriteLine("process scrollview");
                    ProcessScrollView(node);
                }
                else
                {
                    switch (ui.UIElement)
                    {
                        case "Button":
                            ProcessButton(node);
                            break;
                        case "Label":
                            ProcessLabel(node);
                            break;
                        case "Image":
                            ProcessImage(node);
                            break;
                        case "Entry":
                            ProcessEntry(node);
                            break;
                        case "TextView":
                            ProcessTextView(node);
                            break;
                        case "Switch":
                            ProcessSwitch(node);
                            break;
                        case "TableView":
                            ProcessTableView(node);
                            break;
                    }

                    ui.UIXPos = dict.GetKeyValue("x", "0");
                    ui.UIYPos = dict.GetKeyValue("y", "0");
                    ui.UIWidth = dict.GetKeyValue("width", "0");
                    ui.UIHeight = dict.GetKeyValue("height", "0");

                    try
                    {
                        var align = dict.GetKeyValue("contentHorizontalAlignment", "Left");
                        ui.TextHAlign = align;

                        align = dict.GetKeyValue("contentVerticalAlignment", "Left");
                        if (!string.IsNullOrEmpty(align))
                            ui.TextVAlign = align;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Cannot set alignment - no biggy");
                    }

                    ui.Opaque = node.Attribute("opaque").Value;

                    Console.WriteLine("Xaml name = {0}", ui.UIElement);

                    OutputUIElement(ui, ui.UIElement == "ScrollView");
                    ui = null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ouch! Xml not nice! - {0}::{1} and {2}", e.Message, e.InnerException, ui?.UIElement);
            }
        }

        void ProcessScrollView(XElement doc)
        {
            Console.WriteLine("generate scrollview");

            var t = new List<XElement> { doc };
            Generator(t, true);
            //CloseScrollView();
        }

        void ProcessTableView(XElement doc)
        {
            var dict = ProcessMakeDict(doc);
        }

        void ProcessLabel(XElement doc)
        {
            var dict = ProcessMakeDict(doc);

            var fontDesc = dict.GetKeyValue("fontDescription");
            if (!string.IsNullOrEmpty(fontDesc))
                ProcessFont(fontDesc, doc);

            var t1 = dict.GetKeyValue("key");
            if (!string.IsNullOrEmpty(t1))
            {
                if (t1 == "Normal")
                {
                    ProcessColor(dict.GetKeyValue("titleColor"), UITypes.Label, doc);
                }
            }

            ui.Text = doc.Attribute("text").Value;

            ui.LineBreakMode = doc.Attribute("lineBreakMode").Value;
            ui.Enabled = ui.LineBreakMode = doc.Attribute("userInteractionEnabled").Value == "NO" ? "false" : "true";
        }

        void ProcessSwitch(XElement doc)
        {
            ui.ContentHAlign = doc.Attribute("contentHorizontalAlignment").Value;
            ui.ContentVAlign = doc.Attribute("contentVerticalAlignment").Value;

        }

        void ProcessButton(XElement doc)
        {
            var dict = ProcessMakeDict(doc);

            var fontDesc = dict.GetKeyValue("fontDescription");
            if (!string.IsNullOrEmpty(fontDesc))
                ProcessFont(fontDesc, doc);

            var color = dict.GetKeyValue("titleColor");
            if (!string.IsNullOrEmpty(color))
                ProcessColor(color, UITypes.Button, doc);

            ui.Text = dict.GetKeyValue("title");

            ui.ContentHAlign = doc.Attribute("contentHorizontalAlignment").Value;
            ui.ContentVAlign = doc.Attribute("contentVerticalAlignment").Value;
            ui.LineBreakMode = doc.Attribute("lineBreakMode").Value;
        }

        void ProcessImage(XElement doc)
        {
            var dict = ProcessMakeDict(doc);
            ui.ImageName = dict.GetKeyValue("image");
        }

        void ProcessEntry(XElement doc)
        {
            var dict = ProcessMakeDict(doc);

            var fontDesc = dict.GetKeyValue("fontDescription");
            if (!string.IsNullOrEmpty(fontDesc))
                ProcessFont(fontDesc, doc);

            var color = dict.GetKeyValue("color");
            if (!string.IsNullOrEmpty(color))
                ProcessColor(color, UITypes.TextField, doc);
        }

        void ProcessTextView(XElement doc)
        {
            var dict = ProcessMakeDict(doc);

            var fontDesc = dict.GetKeyValue("fontDescription");
            if (!string.IsNullOrEmpty(fontDesc))
                ProcessFont(fontDesc, doc);

            var colText = dict.GetKeyValue("titleColor");
            if (!string.IsNullOrEmpty(colText))
            {
                var col = dict.GetKeyValue("red", "0");
                if (!string.IsNullOrEmpty(col))
                    ui.ColorR = ((int)(Convert.ToDouble(col) * 255)).ToString();
                col = dict.GetKeyValue("green", "0");
                if (!string.IsNullOrEmpty(col))
                    ui.ColorG = ((int)(Convert.ToDouble(col) * 255)).ToString();
                col = dict.GetKeyValue("blue", "0");
                if (!string.IsNullOrEmpty(col))
                    ui.ColorB = ((int)(Convert.ToDouble(col) * 255)).ToString();
                col = dict.GetKeyValue("alpha", "1");
                if (!string.IsNullOrEmpty(col))
                    ui.ColorA = col;
                col = dict.GetKeyValue("white", "0");
                if (!string.IsNullOrEmpty(col))
                {
                    ui.ColorW = col;
                    ui.ColorB = ui.ColorG = ui.ColorR = "255";
                }
            }

            colText = dict.GetKeyValue("backgroundColor");
            if (!string.IsNullOrEmpty(colText))
            {
                var col = dict.GetKeyValue("red", "0");
                if (!string.IsNullOrEmpty(col))
                    ui.BGColorR = ((int)(Convert.ToDouble(col) * 255)).ToString();
                col = dict.GetKeyValue("green", "0");
                if (!string.IsNullOrEmpty(col))
                    ui.BGColorG = ((int)(Convert.ToDouble(col) * 255)).ToString();
                col = dict.GetKeyValue("blue", "0");
                if (!string.IsNullOrEmpty(col))
                    ui.BGColorB = ((int)(Convert.ToDouble(col) * 255)).ToString();
                col = dict.GetKeyValue("alpha", "1");
                if (!string.IsNullOrEmpty(col))
                    ui.BGColorA = col;
                col = dict.GetKeyValue("white", "0");
                if (!string.IsNullOrEmpty(col))
                {
                    ui.BGColorW = col;
                    ui.BGColorB = ui.BGColorG = ui.BGColorR = "255";
                }
            }
        }

        NameValueCollection ProcessMakeDict(XElement doc)
        {
            var dict = new NameValueCollection
            {
                { "id", doc.Attribute("id").Value }
            };

            var elems = doc.Elements();

            foreach (var r in elems)
            {
                if (r.HasElements)
                {
                    var inner = r.Elements();
                    var attrInner = inner.Attributes();
                    foreach (var a in attrInner)
                    {
                        dict.Add(a.Name.ToString(), a.Value);
                    }
                }
                else
                {
                    var attr = r.Attributes();
                    foreach (var a in attr)
                    {
                        dict.Add(a.Name.ToString(), a.Value);
                    }
                }
            }

            return dict;
        }

        void ProcessFont(string doc, XElement dc)
        {
            var dict = ProcessMakeDict(dc);

            var fontType = dict.GetKeyValue("type");
            if (fontType.Contains("bold"))
                ui.FontStyle = "Bold";
            if (fontType.Contains("italic"))
                ui.FontStyle = "Italic";

            ui.FontSize = dict.GetKeyValue("pointSize", "14");
        }

        void ProcessColor(string doc, UITypes type, XElement dc)
        {
            var dict = ProcessMakeDict(dc);
            var val = dict.GetKeyValue("key");

            switch (type)
            {
                case UITypes.Label:
                    if (!string.IsNullOrEmpty(val))
                    {
                        if (val == "textColor")
                        {
                            var colType = dict.GetKeyValue("cocoaTouchSystemColor");
                            if (!string.IsNullOrEmpty(colType))
                            {
                                ProcessSystemColor(colType);
                            }
                        }
                    }

                    break;
                case UITypes.Button:
                    if (val == "titleColor")
                    {
                        var col = dict.GetKeyValue("red", "0");
                        if (!string.IsNullOrEmpty(col))
                            ui.ColorR = ((int)(Convert.ToDouble(col) * 255)).ToString();
                        col = dict.GetKeyValue("green", "0");
                        if (!string.IsNullOrEmpty(col))
                            ui.ColorG = ((int)(Convert.ToDouble(col) * 255)).ToString();
                        col = dict.GetKeyValue("blue", "0");
                        if (!string.IsNullOrEmpty(col))
                            ui.ColorB = ((int)(Convert.ToDouble(col) * 255)).ToString();
                        col = dict.GetKeyValue("alpha", "1");
                        if (!string.IsNullOrEmpty(col))
                            ui.ColorA = col;
                        col = dict.GetKeyValue("white", "0");
                        if (!string.IsNullOrEmpty(col))
                        {
                            ui.ColorW = col;
                            ui.ColorB = ui.ColorG = ui.ColorR = "255";
                        }
                    }

                    break;
            }
        }

        void ProcessRGBAColor(List<string> col)
        {
            if (col.Count > 1)
            {
                ui.ColorA = col[3];
                ui.ColorR = col[0];
                ui.ColorG = col[1];
                ui.ColorB = col[2];
            }
            else
                ui.ColorW = col[0];
        }

        void ProcessSystemColor(string colType)
        {
            switch (colType)
            {
                case "darkTextColor":
                case "blackColor":
                    ui.ColorR = ui.ColorB = ui.ColorG = "0";
                    break;
                case "lightTextColor":
                case "clearColor":
                    ui.ColorR = ui.ColorB = ui.ColorG = "0";
                    ui.ColorA = colType == "clearColor" ? "0" : "0.6";
                    break;
                case "darkGrayColor":
                    ui.ColorR = ui.ColorB = ui.ColorG = "84";
                    break;
                case "lightGrayColor":
                case "tableSeparatorDarkColor":
                    ui.ColorR = ui.ColorB = ui.ColorG = "170";
                    break;
                case "grayColor":
                case "tableCellGrayTextColor":
                    ui.ColorR = ui.ColorB = ui.ColorG = "127";
                    break;
                case "redColor":
                    ui.ColorR = "255";
                    ui.ColorB = ui.ColorG = "0";
                    break;
                case "greenColor":
                    ui.ColorG = "255";
                    ui.ColorB = ui.ColorR = "0";
                    break;
                case "blueColor":
                    ui.ColorB = "255";
                    ui.ColorR = ui.ColorG = "0";
                    break;
                case "cyanColor":
                    ui.ColorG = ui.ColorB = "255";
                    ui.ColorR = "0";
                    break;
                case "yellowColor":
                    ui.ColorR = ui.ColorG = "255";
                    ui.ColorB = "0";
                    break;
                case "magentaColor":
                    ui.ColorR = ui.ColorB = "255";
                    ui.ColorG = "0";
                    break;
                case "orangeColor":
                    ui.ColorR = "255";
                    ui.ColorG = "127";
                    ui.ColorB = "0";
                    break;
                case "purpleColor":
                    ui.ColorR = ui.ColorB = "127";
                    ui.ColorG = "0";
                    break;
                case "brownColor":
                    ui.ColorR = "153";
                    ui.ColorG = "102";
                    ui.ColorB = "51";
                    break;
                case "tableSeparatorLightColor":
                    ui.ColorR = ui.ColorB = ui.ColorG = "224";
                    break;
                case "tableBackgroundColor":
                case "tableCellPlainBackgroundColor":
                case "tableCellGroupedBackgroundColor":
                    ui.ColorR = ui.ColorB = ui.ColorG = "255";
                    break;
                case "tableSelectionColor":
                    ui.ColorR = "40";
                    ui.ColorG = "110";
                    ui.ColorB = "212";
                    break;
                case "selectionListBorderColor":
                    ui.ColorR = "133";
                    ui.ColorG = "143";
                    ui.ColorB = "148";
                    ui.ColorA = "0.6";
                    break;
                case "selectionHeaderBackgroundColor":
                    ui.ColorR = "230";
                    ui.ColorG = "237";
                    ui.ColorB = "252";
                    break;
                case "selectionHeaderBorderColor":
                    ui.ColorR = "217";
                    ui.ColorG = "222";
                    ui.ColorB = "232";
                    break;
                case "tableCellBlueTextColor":
                    ui.ColorR = "56";
                    ui.ColorG = "84";
                    ui.ColorB = "135";
                    break;
                case "textFieldAtomBlueColor":
                    ui.ColorR = "41";
                    ui.ColorG = "87";
                    ui.ColorB = "255";
                    break;
                case "textFieldAtomPurpleColor":
                    ui.ColorR = "105";
                    ui.ColorG = "0";
                    ui.ColorB = "189";
                    break;
                case "infoTextOverPinStripeTextColor":
                    ui.ColorR = "77";
                    ui.ColorG = "87";
                    ui.ColorB = "107";
                    break;
                case "tableCellValue1BlueColor":
                    ui.ColorR = "56";
                    ui.ColorG = "84";
                    ui.ColorB = "135";
                    break;
                case "tableCellValue2BlueColor":
                    ui.ColorR = "82";
                    ui.ColorG = "102";
                    ui.ColorB = "145";
                    break;
                case "tableGroupedSeparatorLightColor":
                case "tableGroupedTopShadowColor":
                    ui.ColorR = ui.ColorB = ui.ColorG = "0";
                    ui.ColorA = colType == "tableGroupedSeparatorLightColor" ? ".18" : ".8";
                    break;
                case "tableShadowColor":
                    ui.ColorR = ui.ColorG = "255";
                    ui.ColorB = "232";
                    break;
                case "selectionTintColor":
                    ui.ColorR = "0";
                    ui.ColorG = "84";
                    ui.ColorB = "166";
                    ui.ColorA = ".2";
                    break;
                case "textCaretColor":
                    ui.ColorR = "105";
                    ui.ColorG = "79";
                    ui.ColorB = "69";
                    break;
                case "selectionCaretColor":
                    ui.ColorR = "66";
                    ui.ColorG = "107";
                    ui.ColorB = "242";
                    break;
                case "selectionHighlightColor":
                    ui.ColorR = "112";
                    ui.ColorG = "149";
                    ui.ColorB = "252";
                    ui.ColorA = ".18";
                    break;
                case "tableSelectionGradientStartColor":
                    ui.ColorR = "5";
                    ui.ColorG = "140";
                    ui.ColorB = "245";
                    break;
                case "tableSelectionGradientEndColor":
                    ui.ColorR = "10";
                    ui.ColorG = "94";
                    ui.ColorB = "232";
                    break;
            }
        }

        private void CreateHeader(bool isMaui, string filename)
        {
            var classpath = filename.LastIndexOf('\\');
            var classname = filename.Substring(classpath + 1);
            using (var writer = File.CreateText(outfile))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
                if (isMaui)
                {
                    writer.WriteLine("<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"");
                }
                else
                {
                    writer.WriteLine("<ContentPage xmlns=\"http://xamarin.com/schemas/2014/forms\"");
                }

                writer.WriteLine("xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"");
                writer.WriteLine($"x:Class=\"{classname}\"");
                writer.WriteLine("Padding=\"20\">");
                writer.WriteLine();
                writer.WriteLine("<AbsoluteLayout>");
                writer.WriteLine();
            }
        }

        private void CreateFooter()
        {
            if (hasScrollview)
                CloseScrollView();

            using (var writer = File.AppendText(outfile))
            {
                writer.WriteLine("</AbsoluteLayout>");
                writer.WriteLine("</ContentPage>");
            }
        }

        void CloseScrollView()
        {
            using (var writer = File.AppendText(outfile))
            {
                writer.WriteLine("</ScrollView>");
                writer.WriteLine();
            }
        }

        private void OutputUIElement(UIObject ui, bool isScrollView = false)
        {
            Console.WriteLine(ui);

            using (var writer = File.AppendText(outfile))
            {
                writer.WriteLine($"<{ui.UIElement}");
                if (!string.IsNullOrEmpty(ui.UIName))
                    writer.WriteLine($"x:Name=\"{ui.UIName}\"");


                switch (ui.UIElement)
                {
                    case "ImageView":
                        writer.WriteLine($"Source=\"{ui.ImageName}\"");
                        break;
                    case "Entry":
                    case "Label":
                    case "TextView":
                        if (!string.IsNullOrEmpty(ui.Text))
                            writer.WriteLine($"Text=\"{ui.Text}\"");
                        if (!string.IsNullOrEmpty(ui.FontSize))
                            writer.WriteLine($"TextSize=\"{ui.FontSize}\"");
                        if (!string.IsNullOrEmpty(ui.FontStyle))
                            writer.WriteLine($"FontFamily=\"{ui.FontStyle}\"");
                        break;
                }

                writer.WriteLine($"AbsoluteLayout.LayoutBounds=\"{ui.UIXPos},{ui.UIYPos},{ui.UIWidth},{ui.UIHeight}\"");

                if (!string.IsNullOrEmpty(ui.BackgroundColor))
                    writer.WriteLine($"BackgroundColor=\"{ui.BackgroundColor}\"");
                if (!string.IsNullOrEmpty(ui.TextColor))
                    writer.WriteLine($"TextColor=\"{ui.TextColor}\"");
                else
                {
                    if (string.IsNullOrEmpty(ui.ColorA))
                    {
                        if (!string.IsNullOrEmpty(ui.ColorB))
                        {
                            int r = (int)(255 * double.Parse(ui.ColorR));
                            int g = (int)(255 * double.Parse(ui.ColorG));
                            int b = (int)(255 * double.Parse(ui.ColorB));
                            writer.WriteLine($"TextColor=\"#{r.ToString("X")}{g.ToString("X")}{b.ToString("X")}\"");
                        }
                    }
                    else
                    {
                        int a = (int)(255 * double.Parse(ui.ColorA));
                        int r = (int)(255 * double.Parse(ui.ColorR));
                        int g = (int)(255 * double.Parse(ui.ColorG));
                        int b = (int)(255 * double.Parse(ui.ColorB));
                        writer.WriteLine(
                            $"TextColor=\"#{a.ToString("X")}{r.ToString("X")}{g.ToString("X")}{b.ToString("X")}\"");
                    }

                }

                if (!isScrollView)
                {
                    writer.WriteLine("/>");
                }
                else
                {
                    writer.WriteLine(">");
                }
                writer.WriteLine();
            }
        }
    }
}
