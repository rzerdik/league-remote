# Third-party notices

League Remote is MIT licensed (see [LICENSE](LICENSE)). It bundles or depends on the software below.

## Riot Games

League Remote includes no Riot Games assets: no sounds, music, art or data files. Sounds and music are read at runtime
from your own League installation. Champion, spell, item and rune images are loaded from Riot's public Data Dragon
service and are not bundled.

League Remote isn't endorsed by Riot Games and doesn't reflect the views or opinions of Riot Games or anyone officially
involved in producing or managing Riot Games properties. Riot Games, and all associated properties are trademarks or
registered trademarks of Riot Games, Inc.

## Font: Inter (bundled)

The phone app bundles the Inter variable font through `@fontsource-variable/inter`.

Copyright (c) 2016 The Inter Project Authors (https://github.com/rsms/inter).
Licensed under the SIL Open Font License, Version 1.1. The license text is at the end of this file.

The font stack also names Apple's SF Pro. It is not bundled. "iOS", "iPhone" and "Liquid Glass" are Apple trademarks,
used here to describe the look.

## Web app (npm)

| Package | License |
|---|---|
| vue | MIT |
| reka-ui | MIT |
| @microsoft/signalr | MIT |
| @lucide/vue | ISC |
| barcode-detector | MIT |
| zxing-wasm (wraps zxing-cpp) | MIT (zxing-cpp: Apache-2.0) |
| @fontsource-variable/inter | OFL-1.1 (Inter) |

Build tools only, not shipped: vite, @vitejs/plugin-vue, vue-tsc, @vue/tsconfig (MIT), typescript (Apache-2.0),
@types/node (MIT).

## Connector (NuGet)

| Package | License |
|---|---|
| Lib.Net.Http.WebPush | MIT |
| QRCoder | MIT |
| Microsoft.Extensions.FileProviders.Embedded, System.IO.Hashing, System.Management | MIT |
| .NET runtime and ASP.NET Core (bundled in the self-contained exe) | MIT |

Tests only, not shipped: xunit, xunit.runner.visualstudio (Apache-2.0), Microsoft.NET.Test.Sdk,
Microsoft.AspNetCore.SignalR.Client, Microsoft.AspNetCore.TestHost, Microsoft.Extensions.TimeProvider.Testing,
coverlet.collector (MIT).

Exact versions are in `Directory.Packages.props` and `web/package-lock.json`.

## SIL Open Font License 1.1 (Inter)

```text
SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007

PREAMBLE
The goals of the Open Font License (OFL) are to stimulate worldwide
development of collaborative font projects, to support the font creation
efforts of academic and linguistic communities, and to provide a free and
open framework in which fonts may be shared and improved in partnership
with others.

The OFL allows the licensed fonts to be used, studied, modified and
redistributed freely as long as they are not sold by themselves. The
fonts, including any derivative works, can be bundled, embedded,
redistributed and/or sold with any software provided that any reserved
names are not used by derivative works. The fonts and derivatives,
however, cannot be released under any other type of license. The
requirement for fonts to remain under this license does not apply
to any document created using the fonts or their derivatives.

DEFINITIONS
"Font Software" refers to the set of files released by the Copyright
Holder(s) under this license and clearly marked as such. This may
include source files, build scripts and documentation.

"Reserved Font Name" refers to any names specified as such after the
copyright statement(s).

"Original Version" refers to the collection of Font Software components as
distributed by the Copyright Holder(s).

"Modified Version" refers to any derivative made by adding to, deleting,
or substituting -- in part or in whole -- any of the components of the
Original Version, by changing formats or by porting the Font Software to a
new environment.

"Author" refers to any designer, engineer, programmer, technical
writer or other person who contributed to the Font Software.

PERMISSION & CONDITIONS
Permission is hereby granted, free of charge, to any person obtaining
a copy of the Font Software, to use, study, copy, merge, embed, modify,
redistribute, and sell modified and unmodified copies of the Font
Software, subject to the following conditions:

1) Neither the Font Software nor any of its individual components,
in Original or Modified Versions, may be sold by itself.

2) Original or Modified Versions of the Font Software may be bundled,
redistributed and/or sold with any software, provided that each copy
contains the above copyright notice and this license. These can be
included either as stand-alone text files, human-readable headers or
in the appropriate machine-readable metadata fields within text or
binary files as long as those fields can be easily viewed by the user.

3) No Modified Version of the Font Software may use the Reserved Font
Name(s) unless explicit written permission is granted by the corresponding
Copyright Holder. This restriction only applies to the primary font name as
presented to the users.

4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font
Software shall not be used to promote, endorse or advertise any
Modified Version, except to acknowledge the contribution(s) of the
Copyright Holder(s) and the Author(s) or with their explicit written
permission.

5) The Font Software, modified or unmodified, in part or in whole,
must be distributed entirely under this license, and must not be
distributed under any other license. The requirement for fonts to
remain under this license does not apply to any document created
using the Font Software.

TERMINATION
This license becomes null and void if any of the above conditions are
not met.

DISCLAIMER
THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT
OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE
COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL
DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM
OTHER DEALINGS IN THE FONT SOFTWARE.
```
