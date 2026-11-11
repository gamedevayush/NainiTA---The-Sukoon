"""Catalogue of every spoken line: Hinglish key (exactly as in game code), Devanagari (for the Hindi
TTS voice) and English (for the English voice). kind: npc (male+female), narrator (female), intro (male).

Usage: python -I voice_lines.py <out_json>
"""
import hashlib
import json
import sys

L = [
    # ---- tourists / locals (NpcWalker.DefaultLines)
    ("npc", "Bhaiya, Naina Devi mandir yahan se seedha Mallital ki taraf hai.", "भैया, नैना देवी मंदिर यहाँ से सीधा मल्लीताल की तरफ़ है।", "Naina Devi temple is straight ahead towards Mallital."),
    ("npc", "Shaam ko Mall Road pe lights dekhna, jheel mein reflection kamaal ka lagta hai!", "शाम को मॉल रोड पे लाइट्स देखना, झील में रिफ़्लेक्शन कमाल का लगता है!", "See the Mall Road lights in the evening - the reflections on the lake are amazing!"),
    ("npc", "Boating Tallital aur Mallital dono jagah se hoti hai.", "बोटिंग तल्लीताल और मल्लीताल, दोनों जगह से होती है।", "You can go boating from both Tallital and Mallital."),
    ("npc", "Snow View tak ropeway jaata hai, upar se Himalaya dikhta hai.", "स्नो व्यू तक रोपवे जाता है, ऊपर से हिमालय दिखता है।", "A ropeway goes up to Snow View - you can see the Himalayas from there."),
    ("npc", "Thandi Sadak pe shaam ko walk karna, bahut shanti hai.", "ठंडी सड़क पे शाम को वॉक करना, बहुत शांति है।", "Take an evening walk on Thandi Sadak, it's very peaceful."),
    ("npc", "Bada Bazar mein candles aur woollen kapde zaroor dekhna.", "बड़ा बाज़ार में कैंडल्स और ऊनी कपड़े ज़रूर देखना।", "Do check out the candles and woollens in Bada Bazar."),
    ("npc", "Tiffin Top (Dorothy's Seat) tak trek karo, view mast hai.", "टिफ़िन टॉप, यानी डोरोथी सीट तक ट्रेक करो, व्यू मस्त है।", "Trek up to Tiffin Top (Dorothy's Seat) - the view is great."),
    ("npc", "Raat mein gali-mohallon mein sambhal ke, kuch gunde ghoomte hain!", "रात में गली-मोहल्लों में संभल के, कुछ गुंडे घूमते हैं!", "Be careful in the back lanes at night, some thugs roam around!"),
    ("npc", "Momos khaye? Mall Road pe best milte hain.", "मोमोज़ खाए? मॉल रोड पे बेस्ट मिलते हैं।", "Had momos yet? Mall Road has the best ones."),
    ("npc", "Yeh jheel aankh ki shape ki hai, isliye Naini naam pada.", "यह झील आँख की शेप की है, इसलिए नैनी नाम पड़ा।", "The lake is shaped like an eye, that's why it's called Naini."),
    ("npc", "Parking ki bahut dikkat hai yahan, gaadi Tallital pe hi laga do.", "पार्किंग की बहुत दिक्कत है यहाँ, गाड़ी तल्लीताल पे ही लगा दो।", "Parking is a real problem here - just park at Tallital."),
    ("npc", "Tibetan market mein sasti jackets milti hain, dekh lena.", "तिब्बती मार्केट में सस्ती जैकेट्स मिलती हैं, देख लेना।", "You get cheap jackets in the Tibetan market, take a look."),
    ("npc", "Bandaron se bach ke, chips cheen ke bhaag jaate hain!", "बंदरों से बच के, चिप्स छीन के भाग जाते हैं!", "Watch out for the monkeys, they snatch chips and run!"),
    ("npc", "Season mein yahan itni bheed hoti hai ki Mall Road pe chalna mushkil.", "सीज़न में यहाँ इतनी भीड़ होती है कि मॉल रोड पे चलना मुश्किल।", "In peak season it's so crowded you can barely walk on Mall Road."),
    # ---- temple, boatmen, shopkeepers
    ("npc", "Jai Maa Naina Devi! Yahan sachche mann se jo maango, milta hai.", "जय माँ नैना देवी! यहाँ सच्चे मन से जो माँगो, मिलता है।", "Jai Maa Naina Devi! Whatever you wish for with a true heart comes true here."),
    ("npc", "Navratri mein yahan bahut bada mela lagta hai.", "नवरात्रि में यहाँ बहुत बड़ा मेला लगता है।", "A huge fair is held here during Navratri."),
    ("npc", "Prasad le lo beta, Maa ka aashirwad hai.", "प्रसाद ले लो बेटा, माँ का आशीर्वाद है।", "Take some prasad, child - it's the Mother's blessing."),
    ("npc", "Naav chahiye saab? Ek ghanta, poori jheel ghuma dunga!", "नाव चाहिए साब? एक घंटा, पूरी झील घुमा दूँगा!", "Need a boat, sir? One hour, I'll take you round the whole lake!"),
    ("npc", "Yacht wali sailing Boat Club se hoti hai, wo alag hai.", "यॉट वाली सेलिंग बोट क्लब से होती है, वो अलग है।", "Yacht sailing is run by the Boat Club, that's separate."),
    ("npc", "Shaam ko jheel sabse sundar lagti hai, abhi le lo naav.", "शाम को झील सबसे सुंदर लगती है, अभी ले लो नाव।", "The lake is prettiest in the evening - take a boat now."),
    ("npc", "Aao ji, garam shawl dekh lo, Kashmir se aaya hai!", "आओ जी, गरम शॉल देख लो, कश्मीर से आया है!", "Come, have a look at these warm shawls - straight from Kashmir!"),
    ("npc", "Nainital ki special candle le jao, ghar pe sab khush ho jayenge.", "नैनीताल की स्पेशल कैंडल ले जाओ, घर पे सब ख़ुश हो जाएँगे।", "Take a special Nainital candle home, everyone will love it."),
    ("npc", "Sahi daam lagaunga, aap toh apne hi ho.", "सही दाम लगाऊँगा, आप तो अपने ही हो।", "I'll give you a fair price, you're one of us."),
    # ---- shouts
    ("npc", "Oye! Kahan ja raha hai?", "ओए! कहाँ जा रहा है?", "Hey! Where do you think you're going?"),
    ("npc", "Arey bhai dekh ke chala!", "अरे भाई, देख के चला!", "Hey, watch where you're driving!"),
    ("npc", "Oye! Andha hai kya?", "ओए! अंधा है क्या?", "Oi! Are you blind?"),
    ("npc", "Police bulaunga!", "पुलिस बुलाऊँगा!", "I'll call the police!"),
    ("npc", "Hai Ram!", "हाय राम!", "Oh my God!"),
    # ---- information boards (narrator)
    ("narrator", "Kumaon ki sabse mashhoor jheel, lagbhag 1,938 m ki unchai par. Aankh (nain) jaise aakar ki - manyata hai ki Mata Sati ki aankh yahan giri thi. Lambai lagbhag 1.4 km. Uttari sira Mallital, dakshini sira Tallital.",
     "कुमाऊँ की सबसे मशहूर झील, लगभग उन्नीस सौ अड़तीस मीटर की ऊँचाई पर। आँख, यानी नैन, जैसे आकार की। मान्यता है कि माता सती की आँख यहाँ गिरी थी। लंबाई लगभग डेढ़ किलोमीटर। उत्तरी सिरा मल्लीताल, दक्षिणी सिरा तल्लीताल।",
     "Kumaon's most famous lake, at about 1,938 metres. Shaped like an eye - legend says Goddess Sati's eye fell here. About 1.4 kilometres long. The north end is Mallital, the south end Tallital."),
    ("narrator", "51 Shaktipeethon mein se ek. Manyata hai ki yahan Mata Sati ki aankhein giri thin. Purana mandir 1880 ke bade bhooskhalan (landslide) mein nasht ho gaya tha, baad mein dobara banaya gaya. Jheel ke uttari kinare, Mallital mein.",
     "इक्यावन शक्तिपीठों में से एक। मान्यता है कि यहाँ माता सती की आँखें गिरी थीं। पुराना मंदिर अठारह सौ अस्सी के बड़े भूस्खलन में नष्ट हो गया था, बाद में दोबारा बनाया गया। झील के उत्तरी किनारे, मल्लीताल में।",
     "One of the 51 Shakti Peethas, where Goddess Sati's eyes are believed to have fallen. The old temple was destroyed in the great landslide of 1880 and later rebuilt."),
    ("narrator", "Mallital mein Naina Devi mandir ke bilkul paas. Shaant mahaul, langar aur jheel ka sundar nazara.",
     "मल्लीताल में नैना देवी मंदिर के बिल्कुल पास। शांत माहौल, लंगर और झील का सुंदर नज़ारा।",
     "Right next to Naina Devi temple in Mallital. Peaceful, with langar and a lovely view of the lake."),
    ("narrator", "Mallital ka purana Catholic church - British daur ki pathar ki imaarat.",
     "मल्लीताल का पुराना कैथोलिक चर्च, ब्रिटिश दौर की पत्थर की इमारत।",
     "An old Catholic church in Mallital - a stone building from the British era."),
    ("narrator", "Uttarakhand ke Rajyapal ka nivas. 1897-1900 ke beech Buckingham Palace ki tarz par Gothic shaili mein bana. Saath mein golf course bhi hai.",
     "उत्तराखंड के राज्यपाल का निवास। अठारह सौ सत्तानवे से उन्नीस सौ के बीच बकिंघम पैलेस की तर्ज़ पर गॉथिक शैली में बना। साथ में गोल्फ़ कोर्स भी है।",
     "Residence of the Governor of Uttarakhand. Built 1897 to 1900 in Gothic style, modelled on Buckingham Palace. It also has a golf course."),
    ("narrator", "Mallital ka purana bazaar - woolen kapde, Nainital ki mashhoor rang-birangi candles aur khaane-peene ki dukaanein.",
     "मल्लीताल का पुराना बाज़ार। ऊनी कपड़े, नैनीताल की मशहूर रंग-बिरंगी कैंडल्स और खाने-पीने की दुकानें।",
     "Mallital's old market - woollens, Nainital's famous colourful candles and food stalls."),
    ("narrator", "1880 ke bade landslide ke malbe se bana khula maidan. Aaj yahan khel, mele aur shaam ki raunak hoti hai.",
     "अठारह सौ अस्सी के बड़े भूस्खलन के मलबे से बना खुला मैदान। आज यहाँ खेल, मेले और शाम की रौनक होती है।",
     "An open ground formed from the debris of the great 1880 landslide. Today it hosts sports, fairs and lively evenings."),
    ("narrator", "Lagbhag 2,270 m unchai. Mallital se ropeway (cable car) se pahunchte hain. Saaf mausam mein Nanda Devi samet Himalaya ki chotiyan dikhti hain.",
     "लगभग बाईस सौ सत्तर मीटर ऊँचाई। मल्लीताल से रोपवे से पहुँचते हैं। साफ़ मौसम में नंदा देवी समेत हिमालय की चोटियाँ दिखती हैं।",
     "About 2,270 metres high, reached by ropeway from Mallital. On clear days you can see Himalayan peaks including Nanda Devi."),
    ("narrator", "Nainital ki sabse oonchi choti, lagbhag 2,615 m. Upar se poora shehar, jheel aur door Himalaya dikhte hain.",
     "नैनीताल की सबसे ऊँची चोटी, लगभग छब्बीस सौ पंद्रह मीटर। ऊपर से पूरा शहर, झील और दूर हिमालय दिखते हैं।",
     "Nainital's highest peak, about 2,615 metres. You can see the whole town, the lake and the distant Himalayas."),
    ("narrator", "Ayarpatta pahadi par. Angrez painter Dorothy Kellett ki yaad mein unke pati ne yahan seat banwayi thi. Picnic ke liye mashhoor.",
     "अयारपाटा पहाड़ी पर। अंग्रेज़ पेंटर डोरोथी केलेट की याद में उनके पति ने यहाँ सीट बनवाई थी। पिकनिक के लिए मशहूर।",
     "On Ayarpatta hill. Her husband built this seat in memory of the English painter Dorothy Kellett. A popular picnic spot."),
    ("narrator", "Yahan se Khurpatal jheel aur gehri ghaati ka shaandaar nazara milta hai.",
     "यहाँ से खुरपाताल झील और गहरी घाटी का शानदार नज़ारा मिलता है।",
     "A grand view of Khurpatal lake and the deep valley below."),
    ("narrator", "Aapas mein judi pathreeli gufaon ka bagicha - gufaon ke naam jaanwaron par hain, jaise Tiger Cave aur Panther Cave.",
     "आपस में जुड़ी पथरीली गुफ़ाओं का बगीचा। गुफ़ाओं के नाम जानवरों पर हैं, जैसे टाइगर केव और पैंथर केव।",
     "A garden of interconnected rocky caves named after animals, like Tiger Cave and Panther Cave."),
    ("narrator", "Pt. G.B. Pant High Altitude Zoo - Sher ka Danda pahadi par, lagbhag 2,100 m unchai par, pahadi jaanwaron aur pakshiyon ka ghar.",
     "पंडित जी बी पंत हाई एल्टीट्यूड ज़ू। शेर का डांडा पहाड़ी पर, लगभग इक्कीस सौ मीटर ऊँचाई पर, पहाड़ी जानवरों और पक्षियों का घर।",
     "Pandit G.B. Pant High Altitude Zoo on Sher ka Danda hill, about 2,100 metres up - home to mountain animals and birds."),
    ("narrator", "Nainital Boat Club - British daur ka purana yacht club. Jheel mein tairti safed paal wali yachts isi ki pehchaan hain.",
     "नैनीताल बोट क्लब, ब्रिटिश दौर का पुराना यॉट क्लब। झील में तैरती सफ़ेद पाल वाली यॉट्स इसी की पहचान हैं।",
     "Nainital Boat Club - a historic yacht club from the British era. The white-sailed yachts on the lake are its trademark."),
    ("narrator", "Mallital se Tallital tak jheel ke kinare ki sadak, sarkari naam Govind Ballabh Pant Marg. Shaam ko lights aur jheel mein unka pratibimb dekhne laayak hai.",
     "मल्लीताल से तल्लीताल तक झील के किनारे की सड़क, सरकारी नाम गोविंद बल्लभ पंत मार्ग। शाम को लाइट्स और झील में उनका प्रतिबिंब देखने लायक है।",
     "The lakeside road from Mallital to Tallital, officially Govind Ballabh Pant Marg. Its evening lights reflected in the lake are worth seeing."),
    ("narrator", "Jheel ka dakshini sira - bus adda, boat stand aur Mall Road ka doosra chhor.",
     "झील का दक्षिणी सिरा। बस अड्डा, बोट स्टैंड और मॉल रोड का दूसरा छोर।",
     "The southern end of the lake - bus stand, boat stand and the other end of Mall Road."),
    ("narrator", "Jheel ka uttari sira - Naina Devi mandir, The Flats, Bada Bazar, Boat Club aur ropeway yahin hain.",
     "झील का उत्तरी सिरा। नैना देवी मंदिर, द फ़्लैट्स, बड़ा बाज़ार, बोट क्लब और रोपवे यहीं हैं।",
     "The northern end of the lake - Naina Devi temple, The Flats, Bada Bazar, the Boat Club and the ropeway are all here."),
    ("narrator", "Bhotia / Tibetan market - woolen jackets, shawls, topi aur sasti cheezein. Mol-bhaav zaroor karna!",
     "भोटिया, यानी तिब्बती मार्केट। ऊनी जैकेट्स, शॉल, टोपी और सस्ती चीज़ें। मोल-भाव ज़रूर करना!",
     "Bhotia or Tibetan market - woollen jackets, shawls, caps and bargains. Don't forget to haggle!"),
    ("narrator", "Thandi Sadak par chattan mein bana chhota sa mandir - chattan par sindoor se Maa ki aakriti. Shaant aur pavitra jagah.",
     "ठंडी सड़क पर चट्टान में बना छोटा सा मंदिर। चट्टान पर सिंदूर से माँ की आकृति। शांत और पवित्र जगह।",
     "A small shrine in the rock on Thandi Sadak - the Goddess's face is painted on the rock in sindoor. Quiet and sacred."),
    ("narrator", "Yahan se naav kiraye pe milti hai. Naav pe E dabao, W/joystick se chalao, A/D se mudo. Wapas kisi bhi jetty pe aakar E dabake utro.",
     "यहाँ से नाव किराए पे मिलती है। नाव पे ई दबाओ, आगे चलाओ, दाएँ बाएँ मोड़ो। वापस किसी भी जेट्टी पे आकर ई दबाकर उतरो।",
     "Boats for hire. Press E at a boat, row forward, turn left or right. Press E at any jetty to get off."),
    # ---- mission intros (male narrator)
    ("intro", "Nainital mein pehla din! Pehle Maa Naina Devi ke darshan, phir kuch garma-garam khaana, phir jheel ka nazara.",
     "नैनीताल में पहला दिन! पहले माँ नैना देवी के दर्शन, फिर कुछ गरमा-गरम खाना, फिर झील का नज़ारा।",
     "First day in Nainital! First visit Maa Naina Devi, then grab some hot food, then enjoy the lake view."),
    ("intro", "Bada Bazar ke dukaandaar pareshaan hain - ek gang hafta vasooli kar raha hai. Unhe sabak sikhao!",
     "बड़ा बाज़ार के दुकानदार परेशान हैं। एक गैंग हफ़्ता वसूली कर रहा है। उन्हें सबक सिखाओ!",
     "The Bada Bazar shopkeepers are being extorted by a gang. Teach them a lesson!"),
    ("intro", "Boat club walon ki challenge: Mallital se Tallital tak naav, saare checkpoints, 4 minute mein!",
     "बोट क्लब वालों का चैलेंज: मल्लीताल से तल्लीताल तक नाव, सारे चेकपॉइंट्स, चार मिनट में!",
     "The boat club's challenge: row from Mallital to Tallital through every checkpoint in 4 minutes!"),
    ("intro", "Taxi stand wale bhaiya beemar hain. Unki taxi chalao: Mallital se sawari uthao aur Tallital chhodo.",
     "टैक्सी स्टैंड वाले भैया बीमार हैं। उनकी टैक्सी चलाओ: मल्लीताल से सवारी उठाओ और तल्लीताल छोड़ो।",
     "The taxi driver is sick. Drive his taxi: pick up a fare at Mallital and drop them at Tallital."),
    ("intro", "Snow View ke cafe wale ka parcel jaldi pahunchana hai - 7 minute hain. Paidal, gaadi, jaise marzi!",
     "स्नो व्यू के कैफ़े वाले का पार्सल जल्दी पहुँचाना है। सात मिनट हैं। पैदल, गाड़ी, जैसे मर्ज़ी!",
     "Deliver a parcel to the Snow View cafe - you have 7 minutes. On foot or by car, your call!"),
    ("intro", "Mandir ke bandar prasad cheen rahe hain! Unki taraf daudo (Shift) aur bhagao.",
     "मंदिर के बंदर प्रसाद छीन रहे हैं! उनकी तरफ़ दौड़ो और भगाओ।",
     "The temple monkeys are snatching offerings! Sprint at them and chase them off."),
    ("intro", "Ek travel blogger ko Nainital ke best viewpoints ki photos chahiye. Sab jagah jaao!",
     "एक ट्रैवल ब्लॉगर को नैनीताल के बेस्ट व्यूपॉइंट्स की फ़ोटोज़ चाहिए। सब जगह जाओ!",
     "A travel blogger needs photos of Nainital's best viewpoints. Visit them all!"),
    ("intro", "Raat mein Tallital pe gunde logon ko loot rahe hain. Police late hai - tum sambhalo!",
     "रात में तल्लीताल पे गुंडे लोगों को लूट रहे हैं। पुलिस लेट है, तुम संभालो!",
     "Thugs are robbing people at Tallital tonight. The police are late - you handle it!"),
]


def key(text: str) -> str:
    return hashlib.md5(text.encode("utf-8")).hexdigest()[:12]


if __name__ == "__main__":
    out = []
    for kind, hinglish, deva, en in L:
        out.append({"key": key(hinglish), "kind": kind, "hinglish": hinglish, "hindi": deva, "english": en})
    with open(sys.argv[1], "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print(len(out), "lines")
