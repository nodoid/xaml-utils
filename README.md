xaml-utils
==========

 xib to xaml converter


Released under the very relaxed and somewhat fun DILLIGAF licence. Feel free to do what you will with it! No guarantees are given and if it doesn't blow your computer up, then you'll be a happy bunny.

Usage xib2xaml -m | -f infile.xib opt-outfile.xaml

Switches (not optional)

-m - MAUI
-f - Xam. Forms

Infile has to be given, if no outfile is specified, the same filename but with .xaml will be generated

Limitations : won't recurse into directories (yet - that comes next)
              it's all held in a AbsoluteLayout
              UIViews, MapViews and TableViews not yet implemented

To Do : Android xml to xaml converter
