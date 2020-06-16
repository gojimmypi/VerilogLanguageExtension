I teach classes on computer hardware security and am an active user of a large number of components as both tools and targets for my own hardware hacking as well as what I teach in class.

As an instructor, I have taught a class on verilog design for security, geared towards ASIC designers. In order to make this a hands-on class, I based it off FPGAs, but had to spend a huge amount of effort concealing the vendor tools that the attendees were completely unfamiliar with and had no interest in learning.

After doing this for 5 years, I taught the class using an open toolchain, and it instantly removed a huge overhead in my course development, and simplified a large impediment to me getting straight to the important educational objectives - exploiting and then securing verilog bugs.

Since then, I have been sold on the open toolchain proposition. Because of that, I'm currently funding work to document the machxo2 bitstream to have it available for open source projects. I don't have a specific project in mind - just that i know i would use it if i had a reliable toolchain, replacing several instances where i use coolrunner CPLDs.

I fully recognize that the hobbyist market represents 0 million units annually and is therefore not even on Lattice's radar, but the state of tools across the market is a reason why microcontrollers are often doing jobs that fpgas are much better suited to. Developers would rather use a solution they're familiar with that has tools they can count on vs. learning something from scratch, with lots of friction in the process, and the risk of their tool license expiring in the middle of their development process.

Studying the history of other industries should make it clear that the end state is a universal, open-source toolchain that can be easily incorporated into any number of development environments. This does mean giving up vendor lock-in and makes it easier for new competitors to enter the market - but trust us, no one likes vendor lock-in except vendors.

From a business strategy perspective, it is better to be on the front end of this change instead of letting either an established or a new competitor break into the space with open tools. In so many cases across industries, products that adapt to this survive, those that don't become obsolete.

One way to support the open toolchain is to document bitstream and share more internal design data. Of course, this might help competitors - but in reality it lowers the cost for the open source movement to support your products, and competitive analysis gets the data anyway. Having competitors spending less time analyzing your hardware is a good thing.

In summary - hobbyists aren't going to make lattice any money by buying FPGAs, but open toolchain developers are providing additional value that will, in the near term, provide a competitive advantage with minimal investment.

-joe

Joe FitzPatrick
joefitz@securinghardware.com
Trainer and Researcher, SecuringHardware.com