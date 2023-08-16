# Forecasting-Container

This will require several Nuget installs, and may be difficult to rearrange into a non-VS context.

DotNetSeleniumExtras.WaitHelpers
HtmlAgilityPack
Selenium.Support
Selenium.WebDriver
Selenium.WebDriver.ChromeDrive (not included with the previous package)

TODO -
There are numerous file paths that should be gathered together into a proper config file way of setting these.

These include paths to a python environment (i.e. to the python.exe executable folder), a path to the root 
code folder where the python script code and the market data and python input/output csv artifacts dwell, 
and the path to your local chrome executable.

These should also include all the URL and XPath snippets, which, in here have been gathered by examination
of secure web pages that I am authorized to see. As-is they represent no security risk to my broker, and no 
unauthorized activity is taking place inthe course of requesting quotes for futures contracts that I am 
entitled to request. To use this application for your own use, you will have to have a source of futures 
price quotes that are authorized to you, and provide the URL and XPath details, and your own credentials for
the automated login/authentication capability. Feel free to observe it in debug mode.

-------------------------------
This scraping application assumes that session IDs are the final authorizing token used by any iframes 
that must be rendered from embedded form URLs in the result of the original quote request. It does not rely 
on any further Splash rendering service, like scrapy, since it runs an actual headless Chrome browser,
trying to operate it exactly as a human person would. No Docker required.

This scrapes at five minute intervals, synced to the 5-minute clock strokes, reasonably closely.

The prediction capability also runs at five minute intervals, but it runs at a fixed interval after the
price scraping and archiving action. That has left an artifact named "CurrentBars.csv", formatted like the 
target archive data file, "5MinuteBars.csv". It tries to run one of two python scripts from another 
repository, ES_FiveFactor, "ES_FiveFactor_FrontOnly.py".

Another fixed interval after that, a python results pick-up method is evented, and puts the python 
prediction script output, dropped in a local file, into the tracking display, along with the latest ES 
front contract price, being tracked in the chart control. The legend displays the model's r-squared.
The point markers are colored red when relatively high, and pale/teal when nearly useless. The lines
represent the current-time r-squared color like the markers.

That python script will need to run some Tensorflow models, and needs to be set up and configured by you, 
if you expect python prediction scripts to be helpful. So you'll need the five models, from that same
python package as the script. That will enable it to run, but no guarantee of the model accuracy as of the 
time you run them. You will want them to actually work well though, so the other script there runs as a 
continuous process, attempting to use your GPU, if you have one. That attempts to improve upon the model 
quality feeding on the data gathered by the "collect" function here.
