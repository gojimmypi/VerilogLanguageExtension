# Lattice Semiconductor Dialog

Discussion on improving relationship with open source community. Why is this important to Lattice? The technical landscape is changing.
More people want open source solutions for long term support and improved security visibility. 

Who is @gojimmypi? I'm here as a maker and a hobbyist. I'm not affiliated with Lattice, or any other chip maker.
I have a Bachelor's in Electronic Engineering, but I work as a senior software engineer at the day job. 

My passion is and always has been electronics. I'm here to try and improve the relationship between the open source community and Lattice Semiconductor.
There's incredibly exciting work going on in the world of FPGAs. I'd like to see the technology become more commonplace and widely available.

Hopefully these free and honest suggestions, with no risk to me regarding company politics or history... can make a difference.

## Summary

Key Items for Lattice to consider:

* Officially embrace the open source communnity and create an "ecosystem" of FPGA products
* Focus on Software Engineers and allow them to use any IDE
* Expand Technical Cooperation and Communication

## Objective

Encourage Lattice Semiconductor to actively engage with the open source community; officially and directly support continued learning opportunities with any IDE.

There's just a 30 minute slot for discussion. If well presented there could be future opportunities for continued dialog.

I'd like to ensure the proper message is well conveyed. I've invited input from several people. (thank you all for the feedback)

## Thank You

Lattice did a quite impressive job of quickly recovering from potentially disastrous PR 
in [their response](https://twitter.com/latticesemi/status/1269115302140231682?s=20) 
to Dave's [outburst](https://twitter.com/fpga_dave/status/1268497428501725184?s=20) over the
change in licensing terms. It seems the entire community expressed their relief and thanks to the response.

Also a personal thank you from @gojimmypi for Lattice not only kindly replying to web form inquiry,
but then also being responsive to subsequent email exchange and arranging this conference call.

## Improved Communication

Lattice Semiconductor is conspicuously missing from the tech community.

Olof Kindgren:
>The story about their licensing and their reversal got _far_ more attention on both LinkedIn and Twitter 
>than any of their recent product announcements. That could be an indication that this is much larger than they think.

Lattice hardly ever "likes" or retweets any of the awesome projects using their hardware. Why?

There's a great opportunity for Lattice to simply show appreciation for all the great projects that showcase their
hardware. (such as [here](https://twitter.com/crowd_supply/status/1242598931869175810?s=20), 
[here](https://twitter.com/digikey/status/1207788724559499264?s=20), and [here](https://twitter.com/xesscorp/status/1255960246511697920?s=20))

Lattice DM's should be open on Twitter. Perhaps someone wants to contact you with a typo on a web page, or a security vulerability. 
Even if the person managing Twitter account does not have all the answers... then simply reply that the question has been passed 
along.

## Documentation

Why not provide bitstream documentation for low-end products like ICE40 and ECP5?
Instead of only "allowing" extraordinarily talented engineers reverse engineer things.
Why not just give out the docs (or be available to help) and let those engineers spend their time on improving the open source tools?

Piotr Esden:
> [I do not understand why you don't see this as an opportunity for your parts and your brand. Don't squander it. Document the bitstream yourself and publish the timing database. Then we don't have to waste time extracting them from your tools](https://twitter.com/esden/status/1268646951693754369?s=20)

## Technical Cooperation

Partial reconfiguration - for example: build a riscv processor and load it in, 
Then change a peripheral and not re-place and route the whole design. (see [Dave's flow example](https://github.com/daveshah1/flow-examples/tree/master/ecp5-partial))
Is this [even possible with the proprietary tool](https://twitter.com/whitequark/status/1167490446718693376?s=20)?

Claire Wolfe: 
>They essentially told us they won't be in our way but they also see no commercial relevance to what we are doing.

Goran Mahovlic (Radiona):
> Open source is readily available for change, so any noticed bug can be fixed instantly. Usually that is not the case with proprietary software especially in FPGA world ... 
> @fpga_dave is often fixing important issues in hours ... Opensource tools does not have any secrets beneath the hood. Some users are then more confident with such tools. 
> Also opensource tool code is available for everyone to improve it

## Development Environment

Lattice should embrace software developers.

As a software engineer, I can attest to the preference to stay within a given development environment. For me - for better or for worse, that's Visual Studio.
I've used Visual Studio since it first arrived on my desk as a beta app on couple of floppy disks. I've used it in my daily professional life 
for _decades_. It is hard to step away from something like that. I'm simply more productive using a tool I am familiar with. 
For my embedded controller development, I was really quite excited to use the [Visual Micro](https://www.visualmicro.com/) extension. 
When I first started learning about FPGA, I was so excited about the capabilities - but also frustrated that I could not use the IDE that I always use... 
so I've spent quite a bit of time writing my own [Verilog Syntax Highlighter Extension](https://marketplace.visualstudio.com/items?itemName=gojimmypi.gojimmypi-verilog-language-extension), and recently
completed [my first FPGA synthesis from within Visual Studio](https://twitter.com/gojimmypi/status/1259634132616744961?s=20). 
From my perspective as a software engineer: _that's how FPGA development is supposed to work_.

My point here? Well I had mixed feelings on yet another proprietary, closed source FPGA development tool. I'll be signing up to learn about Propel, and I'm 
sure it is quite awesome. But there's an entire target audience ... a vastly larger _software_ developer audience that would like to use _other_ tools, too. 
I know some FPGA developers that are using VI/VIM. I'm a Visual Studio developer. Others use VSCode, etc. Why not actively include all these people?

The easier Lattice makes it for developers to create solutions with their hardware, the more customers Lattice will have. Microsoft has known this for years.
The single most important relationship is with the developers. Admittedly this may not be as obvious to a hardware chip manufacturer. But FPGAs are
not your typical hardware. And for FPGAs - it is all about the software: the more options, the better.



## Direct Financial Support

Ideas such as:

* Continue being a sponsoring member of RISC-V foundation
* Sponsor FOSSi Foundation Events
* University Student Grant Program
* Grants for specific projects
* More Contests and Awards such as the [RISC-V SoftCPU Contest](https://riscv.org/2018/10/risc-v-contest/)
* Individual Patreon support (e.g. [@fpga_dave](https://www.patreon.com/fpga_dave))

## Lower Cost

The official Lattice evaluation boards are unreasonably expensive. In a way, that's great for the open source community in being
able to provide much lower cost boards. For instance: the [HM01B0 UPduino Shield](https://www.latticesemi.com/products/developmentboardsandkits/himaxhm01b0)
First, name leaves quite a bit to be desired. The cost: $115? Ouch. With a lead time of 31 weeks? Double ouch.

I personally would never buy that board at that cost. There could be a "limit 1" purchase, at say $25. It's ok to sell these
at a loss. That's the whole point of _evaluation_ board, no?

## Free

Many things can be done at practically zero financial cost, but with tremendous indirect value and show of good will.

The most important free thing is open and visible cooperation with the open source customers. Retweet a project that is using
a Lattice FPGA. "Like" some tweets where @latticesemi is mentioned.

Marketing tweets are typically... well, not very interesting. They are ads. We are flooded and overwhelmed with ads. 
There are nearly 7,000 twitter followers but hardly more than a few likes per @latticesemi ~~ad~~ tweet. 

Engagement tweets are much more interesting. Congratulating some student that won an award with a project using a Lattice product:
Now that's cool. Tweet a picture of a lab, or staff. Post some code samples in a blog. Focus on a maker.

There was a [part 1 blog](https://twitter.com/latticesemi/status/1216914336796553216?s=20) back in January. It started out great... 
but then. Nothing. No code samples. No getting started. Not even a "where to go next". 

Perhaps a Lattice Engineer could show up on discord once in a while? They would we welcomed with open arms! 
Or here's an exciting idea: how about hosting a Lattice AMA (ask me anything)! Do something like that, for an hour or two - say, every quarter or so. 
Hear first hand what your customers have to say. Engage in dialog. Find out what they want.

### Web Site 

Promote community created evaluation and development boards; Add a section (or better: an entire page) dedicated to Open Source projects
https://www.latticesemi.com/solutionsearch?qiptype=982db688d64345bbb3af29e62fee1dc3&active=board

Featuring projects such as:

* https://www.crowdsupply.com/radiona/ulx3s
* https://www.crowdsupply.com/1bitsquared/icebreaker-fpga
* https://groupgets.com/campaigns/710-orangecrab
* https://tinyfpga.com/ 
* https://www.crowdsupply.com/1bitsquared/glasgow [Talking to a SPI flash chip via a JTAG link though an ECP5.](https://twitter.com/GregDavill/status/1253956689914507265)
* https://github.com/pergola-fpga/pergola [Hello world](https://twitter.com/kbeckmann/status/1203348699130335232?s=20)
* https://github.com/greatscottgadgets/luna [on Twitter](https://twitter.com/ktemkin/status/1236221378417680385)

## FPGA Open Source Champions

Have a good working relationship with:

* @enjoy_digital
* @esden
* @fpga_dave
* @ktemkin
* @matthewvenn
* @mithro
* @oe1cxw
* @OlofKindgren
* @Obijuan_cube
* @RadionaOrg
* @scanlime
* @whitequark

... and others

## Benefits

This is what it is all about, right? What is the _benefit_ to Lattice Semiconductor?

Education and reach. Why not have Lattice chips be the _de facto standard_ in all university and other educational environments? 
Why let some other company take this role? Schools don't want expensive, proprietary tools. If lattice doesn't step into
this role [someone else will](https://twitter.com/OlofKindgren/status/1268612294042247169?s=20).

If there's a comfort zone for students with a particular technology, of course they will likely stick with that technology in
their next project and with their next employer.

That's how the [ULX3S](https://radiona.org/ulx3s/) got started; The Lattice FPGA is used by over 330 [University Students](http://skriptarnica.hr/vijest.aspx?newsID=1466)
_every year_ just in Croatia. Now there's a new company formed after a successful Crowd Supply campaign to get this board into the hands of
more people all over the world. The more boards they sell, the more FPGA chips that Lattice sells. The more people that 
like using Lattice chips, the more people that will be more likely to use that same technology in their own project. 

Here's another university example: [FPGA Master Class in SEEE using Open Source Hardware and Software](https://www.dit.ie/electricalelectronicengineering/news/) in Dublin. 

One of the comments I received when putting together these notes:

> my fear is that they'll go all for "we see no commercial importance"

There's "commercial importance" well beyond the quarterly report and balance sheet.

Evidence that the community *wants* Lattice to be successful; Piotr Esden:
> [Documented bitstream and a timing database will allow us to focus on creating new applications and products using 
@latticesemi parts. And we will keep recommending you to our peers](https://twitter.com/esden/status/1268646952708628481?s=20)

Olof Kindgren:
>[the open source tooling was their way into education and research which would increase brand awareness and sales](https://twitter.com/OlofKindgren/status/1268612294042247169?s=20)

By embracing the open source community, Lattice benefits by having a worldwide enthusiastic team promoting their products. 
Dollars cannot buy that kind of marketing value. 

It's all about the ecosystem.

