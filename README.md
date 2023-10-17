xaml-utils
==========

 xib to xaml converter


Released under the very relaxed and somewhat fun DILLIGAF licence. Feel free to do what you will with it! No guarantees are given and if it doesn't blow your computer up, then you'll be a happy bunny.

Usage <tt>xib2xaml -m | -f infile.xib opt-outfile.xaml</tt>

Switches (not optional)

<tt>-m</tt> - MAUI <br />
<tt>-f</tt> - Xam. Forms

Infile has to be given, if no outfile is specified, the same filename but with .xaml will be generated

Limitations : won't recurse into directories (yet - that comes next)<br />
              it's all held in a AbsoluteLayout<br />
              UIViews, MapViews and TableViews not yet implemented

To Do : Android xml to xaml converter
