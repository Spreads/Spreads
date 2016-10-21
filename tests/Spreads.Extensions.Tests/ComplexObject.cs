// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Linq;
using Spreads.Collections;

namespace Spreads.Extensions.Tests {

    public class MyTestClass {
        public string Text { get; set; }
        public decimal Number { get; set; }
    }


    public class ComplexObject {
        public long[] IntArray { get; set; }
        public SortedMap<DateTime, double> SortedMap { get; set; }
        public string TextValue { get; set; }
        public override bool Equals(object obj)
        {
            var co = obj as ComplexObject;
            if (co == null) return false;
            return IntArray.SequenceEqual(co.IntArray) && SortedMap.Keys.SequenceEqual(co.SortedMap.Keys)
                && SortedMap.Values.SequenceEqual(co.SortedMap.Values) && TextValue.Equals(co.TextValue);
        }
        public static ComplexObject Create()
        {
            var rng = new System.Random();
            var sm = new SortedMap<DateTime, double>();
            var ia = new long[1000];
            for (int i = 0; i < 1000; i++) {
                ia[i] = i * 1000;
                sm.Add(DateTime.UtcNow.Date.AddDays(i), Math.Round(i + rng.NextDouble(), 4));

            }
            return new ComplexObject
            {
                IntArray = ia,
                SortedMap = sm,
                // (c) Bloomberg, http://www.bloomberg.com/news/articles/2015-07-09/greeks-under-gun-to-produce-a-reform-plan-to-keep-euro
                // used only for a text sample
                TextValue = @"Prime Minister Alexis Tsipras is under pressure to show how far he’s willing to go to keep Greece in the euro as European leaders express skepticism he can deliver tough reforms.
With a cacophony of voices predicting a possible exit of Greece from the currency,
                    the government has until Thursday midnight to present an economic plan that includes spending cuts, in exchange for a new bailout.Pressure is also mounting on Greece’s creditors to make the country’s debt more sustainable, giving it a chance to rebound from a crisis that has slashed a quarter of its economy.
European Central Bank President Mario Draghi in a Corriere newspaper interview published Thursday characterized the Greek situation as “really difficult.” The continent’s most - indebted country has never been closer to leaving the currency after more than six European leaders made clear this is its last chance.Failure to get a deal could result in the ECB cutting funds to Greek banks, forcing the country to issue IOUs or some other form of exchange to prevent economic collapse.
“A few months ago I wouldn’t have believed that Greece would leave the euro area,” ECB Governing Council member Ardo Hansson said in an interview in Estonian newspaper Postimees. “Now Greece’s situation is much worse than it was even 10 days ago.The situation isn’t hopeless but it is quickly and sharply deteriorating.”German Chancellor Angela Merkel, who as head of Europe’s biggest economy carries the most sway, is doubtful Tsipras will deliver a credible plan, and is willing to accept a Greek exit, according to two government officials familiar with her strategy asking not to be identified discussing internal deliberations.
Market Reaction
Market reaction remained muted with the benchmark Stoxx Europe 600 Index rising 1.8 percent as of 1:30 p.m. Brussels time and the euro sliding 0.2 percent to $1.106. While Greek bonds continued their decline, Portuguese and Italian bonds rose on optimism Greece would be able to reach a deal, or that the ECB would act to protect other markets.
Pressure is also mounting on Merkel and other European leaders to a strike deal.
The government has until Thursday midnight to present an economic plan
“Realistic proposal from Athens needs to be matched by realistic proposal from creditors on debt sustainability to create win-win situation,” European Union President Donald Tusk said in a Twitter post Thursday.
In the U.S., Treasury Secretary Jacob J. Lew called for an agreement that keeps the euro area intact, saying a long-term solution must include restructuring of Greece’s debt.
“It is in the best interest of all parties to find a resolution,” Lew said Wednesday at a Brookings Institution event in Washington. “Greece needs a path towards a sustainable debt path.”
Sunday Gathering
The leaders of all 28 European Union countries will meet in Brussels on Sunday to decide their response to Greece’s proposals.
The ECB said Wednesday it was leaving the level of aid to Greek banks unchanged. It will meet on Monday to consider its own next moves. Greece, meanwhile, extended its bank holiday and capital controls through Monday.Sunday’s gathering represents one of the biggest tests of a five-year-long effort to contain Greece’s debts, which exceed 170 percent of gross domestic product.
Even if there is a deal this Sunday, the country’s exit from the currency bloc is more likely than not for the next two to three years, according to Citigroup Inc. Chief Economist Willem Buiter.
Syriza Capitulation
Securing a deal with creditors will almost certainly require Tsipras and his Coalition of the Radical Left, or Syriza, to capitulate to changes they have resisted since coming to power in January. Greek voters emphatically rejected a program of spending cuts and tax hikes in a July 5 referendum.
On Wednesday, Greece sent a letter to the European Stability Mechanism, the entity that co-ordinates financial aid to member states, requesting a three-year bailout loan.
After months of often-contentious interactions with creditors, the document signed by the new finance minister, Euclid Tsakalotos, struck a conciliatory tone. It said Greece planned to honor all its debts and introduce tax and pension reforms as soon as next week. Tsipras had earlier characterized pension cuts as one of the “red lines” Greece wouldn’t cross.
“Judging from the recent request letter for a new three-year ESM program, Tsipras may be ready for flexibility,” Jacob Kirkegaard, a senior fellow at the Peterson Institute in Washington, said in a blog post.
Stiff Opposition
Still, Tsipras faces stiff opposition from within his ruling party.
“Greece is obviously working to secure an immediate deal, but it must be a deal that opens a window out of the current crisis,” Energy Minister Panagiotis Lafazanis, a hard-line Syriza member, said at a conference in Athens Thursday. “We don’t want a third memorandum with tough austerity measures.”
The ESM has begun the formal process of reviewing the Greek government’s request, which will be followed with more details by the end of Thursday.
“We cannot rule out a deal on Sunday but strong conditionality and commitment to reforms will be required,” analysts at Societe Generale wrote in a note to clients. “Any agreement on potential debt relief -- more likely debt re-profiling -- will only be decided in October, after all reforms have been adopted by the Greek Parliament. The latter condition will be extremely difficult for the Greek government.”
Prime Minister Alexis Tsipras is under pressure to show how far he’s willing to go to keep Greece in the euro as European leaders express skepticism he can deliver tough reforms.
With a cacophony of voices predicting a possible exit of Greece from the currency,
                    the government has until Thursday midnight to present an economic plan that includes spending cuts, in exchange for a new bailout.Pressure is also mounting on Greece’s creditors to make the country’s debt more sustainable, giving it a chance to rebound from a crisis that has slashed a quarter of its economy.
European Central Bank President Mario Draghi in a Corriere newspaper interview published Thursday characterized the Greek situation as “really difficult.” The continent’s most - indebted country has never been closer to leaving the currency after more than six European leaders made clear this is its last chance.Failure to get a deal could result in the ECB cutting funds to Greek banks, forcing the country to issue IOUs or some other form of exchange to prevent economic collapse.
“A few months ago I wouldn’t have believed that Greece would leave the euro area,” ECB Governing Council member Ardo Hansson said in an interview in Estonian newspaper Postimees. “Now Greece’s situation is much worse than it was even 10 days ago.The situation isn’t hopeless but it is quickly and sharply deteriorating.”German Chancellor Angela Merkel, who as head of Europe’s biggest economy carries the most sway, is doubtful Tsipras will deliver a credible plan, and is willing to accept a Greek exit, according to two government officials familiar with her strategy asking not to be identified discussing internal deliberations.
Market Reaction
Market reaction remained muted with the benchmark Stoxx Europe 600 Index rising 1.8 percent as of 1:30 p.m. Brussels time and the euro sliding 0.2 percent to $1.106. While Greek bonds continued their decline, Portuguese and Italian bonds rose on optimism Greece would be able to reach a deal, or that the ECB would act to protect other markets.
Pressure is also mounting on Merkel and other European leaders to a strike deal.
The government has until Thursday midnight to present an economic plan
“Realistic proposal from Athens needs to be matched by realistic proposal from creditors on debt sustainability to create win-win situation,” European Union President Donald Tusk said in a Twitter post Thursday.
In the U.S., Treasury Secretary Jacob J. Lew called for an agreement that keeps the euro area intact, saying a long-term solution must include restructuring of Greece’s debt.
“It is in the best interest of all parties to find a resolution,” Lew said Wednesday at a Brookings Institution event in Washington. “Greece needs a path towards a sustainable debt path.”
Sunday Gathering
The leaders of all 28 European Union countries will meet in Brussels on Sunday to decide their response to Greece’s proposals.
The ECB said Wednesday it was leaving the level of aid to Greek banks unchanged. It will meet on Monday to consider its own next moves. Greece, meanwhile, extended its bank holiday and capital controls through Monday.Sunday’s gathering represents one of the biggest tests of a five-year-long effort to contain Greece’s debts, which exceed 170 percent of gross domestic product.
Even if there is a deal this Sunday, the country’s exit from the currency bloc is more likely than not for the next two to three years, according to Citigroup Inc. Chief Economist Willem Buiter.
Syriza Capitulation
Securing a deal with creditors will almost certainly require Tsipras and his Coalition of the Radical Left, or Syriza, to capitulate to changes they have resisted since coming to power in January. Greek voters emphatically rejected a program of spending cuts and tax hikes in a July 5 referendum.
On Wednesday, Greece sent a letter to the European Stability Mechanism, the entity that co-ordinates financial aid to member states, requesting a three-year bailout loan.
After months of often-contentious interactions with creditors, the document signed by the new finance minister, Euclid Tsakalotos, struck a conciliatory tone. It said Greece planned to honor all its debts and introduce tax and pension reforms as soon as next week. Tsipras had earlier characterized pension cuts as one of the “red lines” Greece wouldn’t cross.
“Judging from the recent request letter for a new three-year ESM program, Tsipras may be ready for flexibility,” Jacob Kirkegaard, a senior fellow at the Peterson Institute in Washington, said in a blog post.
Stiff Opposition
Still, Tsipras faces stiff opposition from within his ruling party.
“Greece is obviously working to secure an immediate deal, but it must be a deal that opens a window out of the current crisis,” Energy Minister Panagiotis Lafazanis, a hard-line Syriza member, said at a conference in Athens Thursday. “We don’t want a third memorandum with tough austerity measures.”
The ESM has begun the formal process of reviewing the Greek government’s request, which will be followed with more details by the end of Thursday.
“We cannot rule out a deal on Sunday but strong conditionality and commitment to reforms will be required,” analysts at Societe Generale wrote in a note to clients. “Any agreement on potential debt relief -- more likely debt re-profiling -- will only be decided in October, after all reforms have been adopted by the Greek Parliament. The latter condition will be extremely difficult for the Greek government.”"
            };
        }

    }
}