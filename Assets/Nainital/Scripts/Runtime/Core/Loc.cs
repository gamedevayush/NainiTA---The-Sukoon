using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Hindi (Roman script) / English switch. All game text is authored in Hinglish; when English is on,
    /// the HUD translates strings at display time (exact matches + patterns for composed text).
    /// </summary>
    public static class Loc
    {
        static int english = -1;   // lazy: PlayerPrefs must not be read from a static initialiser
        public static bool English
        {
            get
            {
                if (english < 0) english = PlayerPrefs.GetInt("nt_lang_en", 0);
                return english == 1;
            }
        }
        public static event System.Action Changed;

        public static void SetEnglish(bool en)
        {
            english = en ? 1 : 0;
            PlayerPrefs.SetInt("nt_lang_en", en ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static readonly Dictionary<string, string> D = new Dictionary<string, string>
        {
            // HUD / menu
            ["Din / Raat"] = "Day / Night", ["Theek hai"] = "OK", ["Wapas khelo"] = "Resume", ["Mausam badlo"] = "Change weather",
            ["Din / Raat badlo"] = "Toggle day / night", ["Game band karo"] = "Quit game", ["Mission chhodo"] = "Abandon mission",
            ["Graphics"] = "Graphics", ["PAUSE"] = "PAUSED", ["Paise"] = "Money", ["Missions"] = "Missions",
            ["Nainital mein swagat hai!  (H = controls, M = map)"] = "Welcome to Nainital!  (H = controls, M = map)",
            ["NAINITAL  (M = band karo)"] = "NAINITAL  (M = close)",
            ["Pura hua"] = "Completed", ["Baaki"] = "Not done",
            // toasts
            ["Kinare pe chadh gaye!"] = "Climbed onto the shore!",
            ["Yahan se nahi chadh sakte. Jetty ya neecha kinara dhundo."] = "Can't climb out here. Find a jetty or a lower bank.",
            ["Aap behosh ho gaye... thodi der mein Mall Road pe hosh aayega."] = "You passed out... you'll wake up on Mall Road shortly.",
            ["Abhi pet bhara hai, thodi der baad aana!"] = "You're full - come back a bit later!",
            ["Jeb khaali hai! Mission karke paise kamao."] = "Your pockets are empty! Earn money from missions.",
            ["Naav ke liye Rs 100 chahiye!"] = "You need Rs 100 for a boat!",
            ["W/Joystick se chalao, A/D se mudo. Jetty pe E dabake utro."] = "W/joystick to row, A/D to turn. Press E at a jetty to get off.",
            ["Mallital ya Tallital boat stand ki jetty ke paas jao."] = "Go near the Mallital or Tallital boat stand jetty.",
            ["Jheel ka nazara... (chalne ke liye move karo)"] = "What a lake view... (move to stand up)",
            ["Subah ho gayi"] = "Good morning!", ["Raat ho gayi - Mall Road ki lights dekho!"] = "Night has fallen - check out the Mall Road lights!",
            ["W/S: race-brake, A/D: steering, Space: handbrake, E: utro"] = "W/S: throttle/brake, A/D: steer, Space: handbrake, E: get out",
            ["Mausam saaf ho gaya"] = "The weather has cleared", ["Baadal chha gaye"] = "Clouds are rolling in",
            ["Kohra (fog) aa gaya - Nainital special!"] = "Fog has rolled in - a Nainital special!",
            ["Baarish shuru! Sadkein geeli hain, dhyan se chalao"] = "It's raining! Roads are wet, drive carefully",
            ["Ye mission sirf raat mein milega. (N dabake raat karo)"] = "This mission is only available at night. (Press N for night)",
            ["Bandar aapke haath se bhutta cheen ke bhaag gaya!"] = "A monkey snatched your corn and ran off!",
            ["Bandar ne chips ka packet uda liya!"] = "A monkey stole your bag of chips!",
            ["Bandar aapka chashma kheench ke le gaya... aur wapas phek diya!"] = "A monkey grabbed your glasses... and threw them back!",
            ["Bandar ne momos wala dona cheen liya!"] = "A monkey snatched your plate of momos!",
            ["Oye! Kahan ja raha hai?"] = "Hey! Where do you think you're going?",
            ["Arey bhai dekh ke chala!"] = "Hey, watch where you're driving!", ["Oye! Andha hai kya?"] = "Oi! Are you blind?",
            ["Police bulaunga!"] = "I'll call the police!", ["Hai Ram!"] = "Oh my God!",
            ["(E dabao)"] = "(press E)",
            // food
            ["Garma garam momos"] = "hot steaming momos", ["Maggi"] = "Maggi noodles", ["Adrak wali chai"] = "ginger tea",
            ["Bun makkhan"] = "bun with butter", ["Bhutta (corn)"] = "roasted corn",
            // interact prompts
            ["Bench pe baitho"] = "Sit on the bench", ["Naav se utro"] = "Get off the boat", ["Utarne ke liye jetty ke paas jao"] = "Go near a jetty to get off",
            ["Naav chalao (Rs 100)"] = "Take a boat (Rs 100)", ["Gaadi se utro"] = "Get out of the car", ["Bus chalao"] = "Drive the bus", ["Gaadi chalao"] = "Drive the car",
            // names
            ["Naini Jheel"] = "Naini Lake", ["Naina Devi Mandir"] = "Naina Devi Temple", ["Pashan Devi Mandir"] = "Pashan Devi Temple",
            ["Tourist (Delhi se)"] = "Tourist (from Delhi)", ["Local Didi"] = "Local lady", ["Honeymoon couple wala"] = "Honeymooner",
            ["Pahadi Dada"] = "Hill grandpa", ["Photographer bhaiya"] = "Photographer", ["Kumaoni Aunty"] = "Kumaoni aunty",
            ["Raahgir"] = "Passer-by", ["Local bhaiya"] = "Local guy", ["Didi"] = "Lady", ["Porter (kuli)"] = "Porter",
            ["Honeymoon couple"] = "Honeymoon couple", ["School bachcha"] = "School kid", ["Bhakt"] = "Devotee", ["Naav wale bhaiya"] = "Boatman",
            ["Dukaandaar"] = "Shopkeeper", ["Tibetan dukaandaar"] = "Tibetan shopkeeper", ["Tourist (view dekh raha)"] = "Tourist (enjoying the view)",
            ["Gunda"] = "Thug", ["Gang ka Boss"] = "Gang boss", ["Raat ka Gunda"] = "Night thug",
            // NPC lines
            ["Bhaiya, Naina Devi mandir yahan se seedha Mallital ki taraf hai."] = "Naina Devi temple is straight ahead towards Mallital.",
            ["Shaam ko Mall Road pe lights dekhna, jheel mein reflection kamaal ka lagta hai!"] = "See the Mall Road lights in the evening - the reflections on the lake are amazing!",
            ["Boating Tallital aur Mallital dono jagah se hoti hai."] = "You can go boating from both Tallital and Mallital.",
            ["Snow View tak ropeway jaata hai, upar se Himalaya dikhta hai."] = "A ropeway goes up to Snow View - you can see the Himalayas from there.",
            ["Thandi Sadak pe shaam ko walk karna, bahut shanti hai."] = "Take an evening walk on Thandi Sadak, it's very peaceful.",
            ["Bada Bazar mein candles aur woollen kapde zaroor dekhna."] = "Do check out the candles and woollens in Bada Bazar.",
            ["Tiffin Top (Dorothy's Seat) tak trek karo, view mast hai."] = "Trek up to Tiffin Top (Dorothy's Seat) - the view is great.",
            ["Raat mein gali-mohallon mein sambhal ke, kuch gunde ghoomte hain!"] = "Be careful in the back lanes at night, some thugs roam around!",
            ["Momos khaye? Mall Road pe best milte hain."] = "Had momos yet? Mall Road has the best ones.",
            ["Yeh jheel aankh ki shape ki hai, isliye Naini naam pada."] = "The lake is shaped like an eye, that's why it's called Naini.",
            ["Parking ki bahut dikkat hai yahan, gaadi Tallital pe hi laga do."] = "Parking is a real problem here - just park at Tallital.",
            ["Tibetan market mein sasti jackets milti hain, dekh lena."] = "You get cheap jackets in the Tibetan market, take a look.",
            ["Bandaron se bach ke, chips cheen ke bhaag jaate hain!"] = "Watch out for the monkeys, they snatch chips and run!",
            ["Season mein yahan itni bheed hoti hai ki Mall Road pe chalna mushkil."] = "In peak season it's so crowded you can barely walk on Mall Road.",
            ["Jai Maa Naina Devi! Yahan sachche mann se jo maango, milta hai."] = "Jai Maa Naina Devi! Whatever you wish for with a true heart comes true here.",
            ["Navratri mein yahan bahut bada mela lagta hai."] = "A huge fair is held here during Navratri.",
            ["Prasad le lo beta, Maa ka aashirwad hai."] = "Take some prasad, child - it's the Mother's blessing.",
            ["Naav chahiye saab? Ek ghanta, poori jheel ghuma dunga!"] = "Need a boat, sir? One hour, I'll take you round the whole lake!",
            ["Yacht wali sailing Boat Club se hoti hai, wo alag hai."] = "Yacht sailing is run by the Boat Club, that's separate.",
            ["Shaam ko jheel sabse sundar lagti hai, abhi le lo naav."] = "The lake is prettiest in the evening - take a boat now.",
            ["Aao ji, garam shawl dekh lo, Kashmir se aaya hai!"] = "Come, have a look at these warm shawls - straight from Kashmir!",
            ["Nainital ki special candle le jao, ghar pe sab khush ho jayenge."] = "Take a special Nainital candle home, everyone will love it.",
            ["Sahi daam lagaunga, aap toh apne hi ho."] = "I'll give you a fair price, you're one of us.",
            // missions
            ["Nainital Darshan"] = "Nainital Darshan", ["Gunda Raj Khatam"] = "End the Goon Raj", ["Naav Race"] = "Boat Race",
            ["Bandar Bhagao"] = "Shoo the Monkeys", ["Raat ki Gasht"] = "Night Patrol",
            ["Nainital mein pehla din! Pehle Maa Naina Devi ke darshan, phir kuch garma-garam khaana, phir jheel ka nazara."] =
                "First day in Nainital! First visit Maa Naina Devi, then grab some hot food, then enjoy the lake view.",
            ["Bada Bazar ke dukaandaar pareshaan hain - ek gang hafta vasooli kar raha hai. Unhe sabak sikhao!"] =
                "The Bada Bazar shopkeepers are being extorted by a gang. Teach them a lesson!",
            ["Boat club walon ki challenge: Mallital se Tallital tak naav, saare checkpoints, 4 minute mein!"] =
                "The boat club's challenge: row from Mallital to Tallital through every checkpoint in 4 minutes!",
            ["Taxi stand wale bhaiya beemar hain. Unki taxi chalao: Mallital se sawari uthao aur Tallital chhodo."] =
                "The taxi driver is sick. Drive his taxi: pick up a fare at Mallital and drop them at Tallital.",
            ["Snow View ke cafe wale ka parcel jaldi pahunchana hai - 7 minute hain. Paidal, gaadi, jaise marzi!"] =
                "Deliver a parcel to the Snow View cafe - you have 7 minutes. On foot or by car, your call!",
            ["Mandir ke bandar prasad cheen rahe hain! Unki taraf daudo (Shift) aur bhagao."] =
                "The temple monkeys are snatching offerings! Sprint at them (Shift) and chase them off.",
            ["Ek travel blogger ko Nainital ke best viewpoints ki photos chahiye. Sab jagah jaao!"] =
                "A travel blogger needs photos of Nainital's best viewpoints. Visit them all!",
            ["Raat mein Tallital pe gunde logon ko loot rahe hain. Police late hai - tum sambhalo!"] =
                "Thugs are robbing people at Tallital tonight. The police are late - you handle it!",
            ["Naina Devi Mandir jaao aur darshan karo"] = "Go to Naina Devi Temple and pay your respects",
            ["Kisi stall se kuch khao (E)"] = "Eat something at a food stall (E)",
            ["Wapas jheel ke kinare aao"] = "Come back to the lake shore",
            ["Bada Bazar pahuncho"] = "Reach Bada Bazar", ["Gang ko harao"] = "Defeat the gang",
            ["Jetty se naav lo (E)"] = "Take a boat from the jetty (E)", ["Checkpoint 1"] = "Checkpoint 1", ["Checkpoint 2"] = "Checkpoint 2",
            ["Tallital jetty pe finish"] = "Finish at the Tallital jetty",
            ["Taxi (ya koi bhi gaadi) mein baitho"] = "Get in the taxi (or any car)",
            ["Mallital se sawari uthao"] = "Pick up the passenger at Mallital",
            ["Sawari ko Tallital Bus Stand chhodo"] = "Drop the passenger at Tallital Bus Stand",
            ["Parcel Snow View pahunchao (E)"] = "Deliver the parcel to Snow View (E)",
            ["3 bandar bhagao (Shift se unki taraf daudo)"] = "Chase off 3 monkeys (sprint at them with Shift)",
            ["Saare viewpoints ki photo lo"] = "Photograph all the viewpoints",
            ["Tallital ke andhere mein gunde dhundo"] = "Find the thugs in the dark at Tallital", ["Gundon ko harao"] = "Defeat the thugs",
            ["MISSION PASSED!"] = "MISSION PASSED!", ["MISSION FAILED"] = "MISSION FAILED",
            ["Aap behosh ho gaye"] = "You passed out", ["Time khatam!"] = "Time's up!", ["Mission chhod diya"] = "Mission abandoned",
            // info boards
            ["Kumaon ki sabse mashhoor jheel, lagbhag 1,938 m ki unchai par. Aankh (nain) jaise aakar ki - manyata hai ki Mata Sati ki aankh yahan giri thi. Lambai lagbhag 1.4 km. Uttari sira Mallital, dakshini sira Tallital."] =
                "Kumaon's most famous lake, at about 1,938 m. Shaped like an eye (nain) - legend says Goddess Sati's eye fell here. About 1.4 km long. The north end is Mallital, the south end Tallital.",
            ["51 Shaktipeethon mein se ek. Manyata hai ki yahan Mata Sati ki aankhein giri thin. Purana mandir 1880 ke bade bhooskhalan (landslide) mein nasht ho gaya tha, baad mein dobara banaya gaya. Jheel ke uttari kinare, Mallital mein."] =
                "One of the 51 Shakti Peethas, where Goddess Sati's eyes are believed to have fallen. The old temple was destroyed in the great landslide of 1880 and later rebuilt. On the north shore of the lake, in Mallital.",
            ["Mallital mein Naina Devi mandir ke bilkul paas. Shaant mahaul, langar aur jheel ka sundar nazara."] =
                "Right next to Naina Devi temple in Mallital. Peaceful, with langar and a lovely view of the lake.",
            ["Mallital ka purana Catholic church - British daur ki pathar ki imaarat."] = "An old Catholic church in Mallital - a stone building from the British era.",
            ["Uttarakhand ke Rajyapal ka nivas. 1897-1900 ke beech Buckingham Palace ki tarz par Gothic shaili mein bana. Saath mein golf course bhi hai."] =
                "Residence of the Governor of Uttarakhand. Built 1897-1900 in Gothic style, modelled on Buckingham Palace. It also has a golf course.",
            ["Mallital ka purana bazaar - woolen kapde, Nainital ki mashhoor rang-birangi candles aur khaane-peene ki dukaanein."] =
                "Mallital's old market - woollens, Nainital's famous colourful candles and food stalls.",
            ["1880 ke bade landslide ke malbe se bana khula maidan. Aaj yahan khel, mele aur shaam ki raunak hoti hai."] =
                "An open ground formed from the debris of the great 1880 landslide. Today it hosts sports, fairs and lively evenings.",
            ["Lagbhag 2,270 m unchai. Mallital se ropeway (cable car) se pahunchte hain. Saaf mausam mein Nanda Devi samet Himalaya ki chotiyan dikhti hain."] =
                "About 2,270 m high, reached by ropeway (cable car) from Mallital. On clear days you can see Himalayan peaks including Nanda Devi.",
            ["Nainital ki sabse oonchi choti, lagbhag 2,615 m. Upar se poora shehar, jheel aur door Himalaya dikhte hain."] =
                "Nainital's highest peak, about 2,615 m. You can see the whole town, the lake and the distant Himalayas.",
            ["Ayarpatta pahadi par. Angrez painter Dorothy Kellett ki yaad mein unke pati ne yahan seat banwayi thi. Picnic ke liye mashhoor."] =
                "On Ayarpatta hill. Her husband built this seat in memory of the English painter Dorothy Kellett. A popular picnic spot.",
            ["Yahan se Khurpatal jheel aur gehri ghaati ka shaandaar nazara milta hai."] = "A grand view of Khurpatal lake and the deep valley below.",
            ["Aapas mein judi pathreeli gufaon ka bagicha - gufaon ke naam jaanwaron par hain, jaise Tiger Cave aur Panther Cave."] =
                "A garden of interconnected rocky caves named after animals, like Tiger Cave and Panther Cave.",
            ["Pt. G.B. Pant High Altitude Zoo - Sher ka Danda pahadi par, lagbhag 2,100 m unchai par, pahadi jaanwaron aur pakshiyon ka ghar."] =
                "Pt. G.B. Pant High Altitude Zoo on Sher ka Danda hill, about 2,100 m up - home to mountain animals and birds.",
            ["Nainital Boat Club - British daur ka purana yacht club. Jheel mein tairti safed paal wali yachts isi ki pehchaan hain."] =
                "Nainital Boat Club - a historic yacht club from the British era. The white-sailed yachts on the lake are its trademark.",
            ["Mallital se Tallital tak jheel ke kinare ki sadak, sarkari naam Govind Ballabh Pant Marg. Shaam ko lights aur jheel mein unka pratibimb dekhne laayak hai."] =
                "The lakeside road from Mallital to Tallital, officially Govind Ballabh Pant Marg. Its evening lights reflected in the lake are worth seeing.",
            ["Jheel ka dakshini sira - bus adda, boat stand aur Mall Road ka doosra chhor."] = "The southern end of the lake - bus stand, boat stand and the other end of Mall Road.",
            ["Jheel ka uttari sira - Naina Devi mandir, The Flats, Bada Bazar, Boat Club aur ropeway yahin hain."] =
                "The northern end of the lake - Naina Devi temple, The Flats, Bada Bazar, the Boat Club and the ropeway are all here.",
            ["Bhotia / Tibetan market - woolen jackets, shawls, topi aur sasti cheezein. Mol-bhaav zaroor karna!"] =
                "Bhotia / Tibetan market - woollen jackets, shawls, caps and bargains. Don't forget to haggle!",
            ["Thandi Sadak par chattan mein bana chhota sa mandir - chattan par sindoor se Maa ki aakriti. Shaant aur pavitra jagah."] =
                "A small shrine in the rock on Thandi Sadak - the Goddess's face is painted on the rock in sindoor. Quiet and sacred.",
            ["Yahan se naav kiraye pe milti hai. Naav pe E dabao, W/joystick se chalao, A/D se mudo. Wapas kisi bhi jetty pe aakar E dabake utro."] =
                "Boats for hire. Press E at a boat, row with W/joystick, turn with A/D. Press E at any jetty to get off.",
        };

        static readonly (Regex re, string en)[] P =
        {
            (new Regex(@"^Padho: (.+)$"), "Read: {0}"),
            (new Regex(@"^(.+) se baat karo$"), "Talk to {0}"),
            (new Regex(@"^(.+) se khao \(Rs (\d+)\)$"), "Eat at {0} (Rs {1})"),
            (new Regex(@"^(.+) khaya! \+(\d+) health  \(-Rs (\d+)\)$"), "Ate {0}! +{1} health (-Rs {2})"),
            (new Regex(@"^Mission: (.+)  \(Rs (\d+), dobara\)$"), "Mission: {0}  (Rs {1}, replay)"),
            (new Regex(@"^Mission: (.+)  \(Rs (\d+)\)$"), "Mission: {0}  (Rs {1})"),
            (new Regex(@"^Shabash! Agla kaam: (.+)$"), "Well done! Next: {0}"),
            (new Regex(@"^Photo kheench li: (.+)$"), "Photo taken: {0}"),
            (new Regex(@"^(.+) dher ho gaya!$"), "{0} is down!"),
            (new Regex(@"^(.+)   \+Rs (\d+)$"), "{0}   +Rs {1}"),
            (new Regex("^(.+): \"(.+)\"$"), "{0}: \"{1}\""),
        };

        public static string T(string s)
        {
            if (!English || string.IsNullOrEmpty(s)) return s;
            if (D.TryGetValue(s, out var en)) return en;
            foreach (var (re, fmt) in P)
            {
                var m = re.Match(s);
                if (!m.Success) continue;
                var args = new object[m.Groups.Count - 1];
                for (int i = 1; i < m.Groups.Count; i++) args[i - 1] = T(m.Groups[i].Value);
                return string.Format(fmt, args);
            }
            return s;
        }
    }
}
