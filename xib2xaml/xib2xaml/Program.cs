using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Specialized;

namespace xib2xaml
{
    class MainClass
    {
        public static void Main(string[] args)
        {
#if !DEBUG
            if (args.Length != 2)
            {
                Console.WriteLine("Usage : xib2xaml <infile>.xib <outfile>.xaml");
                Environment.Exit(-1);
            }

            var convert = new Converter(args[0], args[1]);
            convert.ConvertFile();
#else
            var convert = new Converter("Test.xib", "Test.xaml");
            convert.ConvertFile();
#endif
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
        Dictionary<string, string> Outlets = new Dictionary<string, string>();
        UIObject ui;

        public Converter(string inf, string outf)
        {
            infile = inf;
            outfile = outf;
        }

        public void ConvertFile()
        {
            if (File.Exists(outfile))
            {
                /*File.Delete(outfile);
                CreateHeader();*/
                Console.WriteLine("{0} already exists", outfile);
                return;
            }
            else
                CreateHeader();

            int NumOfViews = 0;
            var ViewIDs = new List<string>();
            string connections = "";
            using (var reader = File.OpenText(infile))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line.Contains("connections"))
                    {
                        var nl = reader.ReadLine();
                        while (!nl.Contains("connections"))
                        {
                            connections += nl + ":";
                            nl = reader.ReadLine();
                        }
                    }
                }
            }

            connections = connections.TrimEnd(':');

            var connect = connections.Split(':').ToList();
            foreach (var cnt in connect)
            {
                if (cnt.Contains("UIView"))
                {
                    NumOfViews++;
                    var doc = XDocument.Parse(cnt);
                    ViewIDs.Add(doc.Root.Attribute("destination").Value);
                }
                else
                {
                    var doc = XDocument.Parse(cnt);
                    var outKey = doc.Root.Attribute("property").Value;
                    var outVal = doc.Root.Attribute("destination").Value;
                    Outlets.Add(outKey, outVal);
                }
            }

            if (NumOfViews == 0)
            {
                Console.WriteLine("Identified element count = {0}", Outlets.Values.Count);
                foreach (var search in Outlets.Values)
                {
                    string node = "";
                    Console.WriteLine("search = {0}", search);
                    using (var reader = File.OpenText(infile))
                    {
                        while (!reader.EndOfStream)
                        {
                            var fullSearch = string.Format("id=\"{0}\"", search);
                            string element = "";
                            var line = reader.ReadLine();
                            if (line.Contains(fullSearch))
                            {
                                try
                                {
                                    var tt = line.Split('<');
                                    element = tt[1].Split(' ')[0];
                                    node += line + "\n";
                                    var nl = reader.ReadLine();
                                    if (!string.IsNullOrEmpty(nl))
                                    {
                                        while (!nl.Contains(element))
                                        {
                                            node += nl + "\n";
                                            nl = reader.ReadLine();
                                        }
                                        node += "</" + element + ">";

                                        ProcessNode(node, fullSearch);
                                        reader.ReadToEnd();
                                    }
                                }
                                catch (NullReferenceException e)
                                {
                                    Console.WriteLine("NullRef thrown ; {0}--{1}", e.Message, e.InnerException);
                                }
                            }
                        }
                    }
                }
                CreateFooter();
            }
        }

        void ProcessNode(string node, string fullSearch)
        {
            ui = new UIObject();
            var dict = ProcessMakeDict(node);

            var ts = fullSearch.Split('"');
            try
            {
                ui.UIName = Outlets.SingleOrDefault(t => t.Value == ts[1]).Key;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cant find key for {0} : {1}-{2}", ts[1], ex.Message, ex.StackTrace);
            }

            try
            {
                var doc = XDocument.Parse(node);

                var docTS = doc.ToString();
                Console.WriteLine("docTS = {0}\n", docTS);
                ui.UIElement = doc.Root.Name.LocalName;
                Console.WriteLine("Element = {0}", ui.UIElement);

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
                    default:
                        ui.UIElement = string.Empty;
                        break;
                }

                Console.WriteLine("XamForms name = {0}", ui.UIElement);

                if (string.IsNullOrEmpty(ui.UIElement))
                {
                    Console.WriteLine("Unknown element type");
                    return;
                }

                ui.UIXPos = dict.GetKeyValue("x", "0");
                ui.UIYPos = dict.GetKeyValue("y", "0");
                ui.UIWidth = dict.GetKeyValue("width", "0");
                ui.UIHeight = dict.GetKeyValue("height", "0");

                var align = dict.GetKeyValue("contentHorizontalAlignment", "Left");
                ui.TextHAlign = align;

                align = dict.GetKeyValue("contentVerticalAlignment", "Left");
                if (!string.IsNullOrEmpty(align))
                    ui.TextVAlign = align;

                ui.Opaque = doc.Root.Attribute("opaque").Value;

                switch (ui.UIElement)
                {
                    case "Button":
                        ProcessButton(doc);
                        OutputUIElement(ui);
                        ui = null;
                        break;
                    case "Label":
                        ProcessLabel(doc);
                        OutputUIElement(ui);
                        ui = null;
                        break;
                    case "Image":
                        ProcessImage(doc);
                        OutputUIElement(ui);
                        ui = null;
                        break;
                    case "Entry":
                        ProcessEntry(doc);
                        OutputUIElement(ui);
                        ui = null;
                        break;
                    case "TextView":
                        ProcessTextView(doc);
                        OutputUIElement(ui);
                        ui = null;
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ouch! Xml not nice! - {0}::{1}", e.Message, e.InnerException);
            }
        }

        void ProcessLabel(XDocument doc)
        {
            var dict = ProcessMakeDict(doc.ToString());

            var fontDesc = dict.GetKeyValue("fontDescription");
            if (!string.IsNullOrEmpty(fontDesc))
                ProcessFont(fontDesc);

            var t1 = dict.GetKeyValue("key");
            if (!string.IsNullOrEmpty(t1))
            {
                if (t1 == "Normal")
                {
                    ProcessColor(dict.GetKeyValue("titleColor"), UITypes.Label);
                }
            }

            ui.Text = doc.Root.Attribute("text").Value;

            ui.LineBreakMode = doc.Root.Attribute("lineBreakMode").Value;
            ui.Enabled = ui.LineBreakMode = doc.Root.Attribute("userInteractionEnabled").Value == "NO" ? "false" : "true";
        }

        void ProcessButton(XDocument doc)
        {
            var dict = ProcessMakeDict(doc.ToString());

            var fontDesc = dict.GetKeyValue("fontDescription");
            if (!string.IsNullOrEmpty(fontDesc))
                ProcessFont(fontDesc);

            var color = dict.GetKeyValue("titleColor");
            if (!string.IsNullOrEmpty(color))
                ProcessColor(color, UITypes.Button);

            ui.Text = dict.GetKeyValue("title");

            ui.ContentHAlign = doc.Root.Attribute("contentHorizontalAlignment").Value;
            ui.ContentVAlign = doc.Root.Attribute("contentVerticalAlignment").Value;
            ui.LineBreakMode = doc.Root.Attribute("lineBreakMode").Value;
        }

        void ProcessImage(XDocument doc)
        {
            var dict = ProcessMakeDict(doc.ToString());
            ui.ImageName = dict.GetKeyValue("image");
        }

        void ProcessEntry(XDocument doc)
        {
            var dict = ProcessMakeDict(doc.ToString());

            var fontDesc = dict.GetKeyValue("fontDescription");
            if (!string.IsNullOrEmpty(fontDesc))
                ProcessFont(fontDesc);

            var color = dict.GetKeyValue("color");
            if (!string.IsNullOrEmpty(color))
                ProcessColor(color, UITypes.TextField);
        }

        void ProcessTextView(XDocument doc)
        {
            var dict = ProcessMakeDict(doc.ToString());

            var fontDesc = dict.GetKeyValue("fontDescription");
            if (!string.IsNullOrEmpty(fontDesc))
                ProcessFont(fontDesc);

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

        NameValueCollection ProcessMakeDict(string doc)
        {
            var ret = doc.Split('\n').ToList();
            var spc = new List<string>();
            foreach (var r in ret)
            {
                if (!string.IsNullOrEmpty(r))
                {
                    var s = r.Split('"')
                     .Select((element, index) => index % 2 == 0  // If even index
                                           ? element.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)  // Split the item
                                           : new string[] { element })  // Keep the entire item
                     .SelectMany(element => element).ToList();
                    
                    for (var t = 1; t < s.Count; ++t)
                    {
                        if (s[t].Contains("="))
                            s[t] = s[t].Remove(s[t].IndexOf("="), 1);

                        if (s[t].Contains("<"))
                            s[t] = s[t].Remove(s[t].IndexOf("<"), 1);

                        if (!string.IsNullOrEmpty(s[t]))
                        {
                            if (!s[t].Contains("<"))
                            {
                                if (s[t] == s.Last())
                                {
                                    if (s[t].Contains("/>"))
                                        spc.Add(s[t].Remove(s[t].Length - 2, 2));
                                    else
                                        spc.Add(s[t].Remove(s[t].Length - 1, 1));
                                }
                                else
                                    spc.Add(s[t]);
                            }
                        }
                    }
                }
            }

            var space = new List<string>();
            foreach (var n in spc)
            {
                if (!string.IsNullOrEmpty(n))
                    space.Add(n);
            }

            var split = new List<string>();
            foreach (var s in space)
            {
                if (!string.IsNullOrEmpty(s))
                    split.AddRange(s.Split('=').ToList());
            }
            var dict = new NameValueCollection();
            var end = split.Count - 1 % 2 == 0 ? split.Count - 1 : split.Count - 2;
            for (var n = 0; n < end; n += 2)
            {
                var first = split[n].Replace("/", "").Replace(">", "") ;
                var last = split[n + 1].Replace("\"", "");
                if (!string.IsNullOrEmpty(first))
                {
                    Console.WriteLine("key = {0}, value = {1}", first, last);
                    dict.Add(first, last);
                }
            }

            return dict;
        }

        void ProcessFont(string doc)
        {
            var dict = ProcessMakeDict(doc);

            var fontType = dict.GetKeyValue("type");
            if (fontType.Contains("bold"))
                ui.FontStyle = "Bold";
            if (fontType.Contains("italic"))
                ui.FontStyle = "Italic";

            ui.FontSize = dict.GetKeyValue("pointSize", "14");
        }

        void ProcessColor(string doc, UITypes type)
        {
            var dict = ProcessMakeDict(doc);
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

        private void CreateHeader()
        {
            using (var writer = File.CreateText(outfile))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
                writer.WriteLine("<ContentPage xmlns=\"http://xamarin.com/schemas/2014/forms\"");
                writer.WriteLine("xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
                writer.WriteLine("x:Class=\"HelloXamarinFormsWorldXaml.AbsoluteLayoutExample\"");
                writer.WriteLine("Padding=\"20\">");
                writer.WriteLine();
                writer.WriteLine("<AbsoluteLayout>");
                writer.WriteLine();
            }
        }

        private void CreateFooter()
        {
            using (var writer = File.AppendText(outfile))
            {
                writer.WriteLine("</AbsoluteLayout>");
                writer.WriteLine("</ContentPage>");
            }
        }

        private void OutputUIElement(UIObject ui)
        {
            Console.WriteLine(ui);

            using (var writer = File.AppendText(outfile))
            {
                writer.WriteLine(string.Format("<{0}", ui.UIElement));
                if (!string.IsNullOrEmpty(ui.UIName))
                    writer.WriteLine(string.Format("x:Name=\"{0}\"", ui.UIName));
                if (ui.UIElement != "imageview")
                {
                    if (!string.IsNullOrEmpty(ui.Text))
                        writer.WriteLine(string.Format("Text=\"{0}\"", ui.Text));
                    if (!string.IsNullOrEmpty(ui.FontSize))
                        writer.WriteLine(string.Format("TextSize=\"{0}\"", ui.FontSize));
                    if (!string.IsNullOrEmpty(ui.FontStyle))
                        writer.WriteLine(string.Format("FontFamily=\"{0}\"", ui.FontStyle));
                }
                else
                {
                    writer.WriteLine(string.Format("Source=\"{0}\""), ui.ImageName);
                }

                writer.WriteLine(string.Format("AbsoluteLayout.LayoutBounds=\"{0},{1},{2},{3}\"", ui.UIXPos, ui.UIYPos, ui.UIWidth, ui.UIHeight));

                //if (ui.UIElement != "scrollView")
                //{
                if (!string.IsNullOrEmpty(ui.BackgroundColor))
                    writer.WriteLine(string.Format("BackgroundColor=\"{0}\"", ui.BackgroundColor));
                if (!string.IsNullOrEmpty(ui.TextColor))
                    writer.WriteLine(string.Format("TextColor=\"{0}\"", ui.TextColor));
                else
                {
                    if (string.IsNullOrEmpty(ui.ColorA))
                    {
                        if (!string.IsNullOrEmpty(ui.ColorB))
                        {
                            int r = (int)(255 * double.Parse(ui.ColorR));
                            int g = (int)(255 * double.Parse(ui.ColorG));
                            int b = (int)(255 * double.Parse(ui.ColorB));
                            writer.WriteLine(string.Format("TextColor=\"#{0}{1}{2}\"", r.ToString("X"), g.ToString("X"), b.ToString("X")));
                        }
                    }
                    else
                    {
                        int a = (int)(255 * double.Parse(ui.ColorA));
                        int r = (int)(255 * double.Parse(ui.ColorR));
                        int g = (int)(255 * double.Parse(ui.ColorG));
                        int b = (int)(255 * double.Parse(ui.ColorB));
                        writer.WriteLine(string.Format("TextColor=\"#{0}{1}{2}{3}\"", a.ToString("X"), r.ToString("X"), g.ToString("X"), b.ToString("X")));
                    }

                }
                //}
                writer.WriteLine("/>");
                writer.WriteLine();
            }
        }
    }
}
