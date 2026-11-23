from docx import Document
from docx.shared import Pt, RGBColor
from docx.enum.table import WD_TABLE_ALIGNMENT

d = Document()
st = d.styles['Normal']; st.font.name = 'Nirmala UI'; st.font.size = Pt(10.5)

def H(t, l=1): d.add_heading(t, l)
def P(t, b=False):
    p = d.add_paragraph(); r = p.add_run(t); r.bold = b; return p
def L(t): d.add_paragraph(t, style='List Bullet')

def M(code, name, when, cut, play, fail='', reward=''):
    d.add_heading(f'{code}  {name}', 3)
    t = d.add_table(rows=0, cols=2); t.style = 'Light Grid Accent 1'; t.alignment = WD_TABLE_ALIGNMENT.LEFT
    rows = [('कब / Unlock', when), ('🎬 Cutscene', cut), ('🎮 Interactive (player खेलता है)', play)]
    if fail: rows.append(('❌ Fail', fail))
    if reward: rows.append(('✅ Reward / आगे', reward))
    for a, b in rows:
        c = t.add_row().cells; c[0].text = a; c[1].text = b
        c[0].paragraphs[0].runs[0].bold = True
        c[0].width = Pt(120)
    d.add_paragraph()

d.add_heading('NAINITAL — STORY & MISSION BIBLE  (v2)', 0)
P('23 Nov 2026 — owner के जवाबों के बाद updated। हर mission के साथ: कब होगा, क्या cutscene में और क्या interactive।')
P('Legend: 🎬 = cutscene (player control नहीं करता)   🎮 = interactive (player चलाता / चुनता / खेलता है)')

H('A. v2 में तय हुए फैसले (LOCKED)')
for x in [
    'Prologue: परिवार नैनीताल घूमने जा रहा था; बेटा भगवान को नहीं मानता; मम्मी: "जिस दिन मिलेंगे, उस दिन पहचान नहीं पाओगे।" सामने से ग़लत आती car से accident, मम्मी-पापा नहीं रहे। फिर नई शुरुआत Mall Road से।',
    'Mysterious Man हमेशा सिर्फ player को दिखता है — घर दिलाने में भी। Landlord कहता है: "आपकी बात तो पहले ही हो चुकी थी।" पड़ोस की खिड़की वाला scene भी सिर्फ player को।',
    'Himalaya Darshan की "छलावा" वाली बात जानबूझकर misdirection है: player (और खेलने वाला) सोचे कि लड़की असली नहीं — सच उल्टा है, वह असली है (Nainital की local, Boss की बेटी)।',
    'लड़की ने spy mode में player को पकड़ लिया था, पर अनजान बनी रही। Tallital पर villa का address जानबूझकर player के सामने पूछती है।',
    'Tallital: गाड़ी दोस्त की है और उसकी ride (सवारी) आई हुई है, इसलिए वह मना करता है। Player और लड़की Tallital → Capitol Cinema roadways/local bus से जाते हैं।',
    'Boat operator दोस्त off-season में side में local owners की cab भी चलाता है।',
    '"तेरी कभी girlfriend बनी है?" / "हाँ, बात तो सही है" — running joke, कई बार आएगा।',
    'Tower 360 (Snow View) पर go-karting और bumper cars सच में हैं — game में बनेंगे।',
    'DL test: Bus Stand → Khurpatal, ZERO touch, ZERO damage (strict), time ≤ baseline × 1.5। पहले car, फिर scooty।',
    'दोस्त की girlfriend Lovers Point के बाद town छोड़ देती है।',
    'Brand नाम नहीं: "Tea-Sutta Bar" → "Chai Kash Cafe" (नया नाम), "Uttarakhand Restaurant" → "Pahadi Rasoi" (नया नाम)।',
    '"Club" = Mall Road का rooftop cafe, रात में music।',
    'Suicide Point: पूरा cutscene, helpline card नहीं।',
    'नाम सुधार: "Capitol Cinema" (Mallital, Flats के पास); Woodland Waterfall "Kaladhungi Road (UT SH 41)" पर है।',
]: L(x)

H('B. Chapter-wise missions — कब, क्या cutscene, क्या interactive')

H('Chapter 0 — Prologue: अकेला सफ़र', 2)
M('0.0', 'The Last Trip', 'Game शुरू — पहला scene।',
  'Car में मम्मी, पापा और बेटा नैनीताल घूमने जा रहे हैं। बेटा: "नैनीताल ही मिला आपको? मनाली चलते!" पापा: "बेटा, यहाँ सीता मैया की आँख गिरी थी।" (नैना देवी) बेटा: "आप लोग क्यों इन चीज़ों पर भरोसा करते हो? कोई किसी का रूप नहीं होता… कोई भगवान, ईश्वर कुछ नहीं होता।" मम्मी: "जिस दिन मिलेंगे, उस दिन पहचान नहीं पाओगे।" तभी सामने से तेज़ car ग़लत side से आती है — टक्कर, गाड़ी खाई के किनारे अटक जाती है, आग लग जाती है। मम्मी-पापा नहीं रहे। (QTE के बाद) Ambulance — बेटे की आँखें बंद होती हैं (black)। (मम्मी की line पूरी कहानी की नींव: uncle = वही "रूप", जिसे player पहचान नहीं पाता।)',
  'QTE — शीशे से बाहर निकलो: गाड़ी में आग, screen पर "Press [E] / tap rapidly to get out"। हर press से escape animation keyframes आगे बढ़ते हैं — जितनी तेज़ press, उतना तेज़ आगे; press रुकते ही animation constant speed से पीछे खिसकता है (आग/धुआँ बढ़ता है)। 100% पर बेटा टूटे शीशे से बाहर रेंगकर निकलता है → ambulance cutscene।',
  'तय समय (आग पूरी फैलने तक) में 100% नहीं → screen black, checkpoint से फिर QTE।')
M('0.0b', 'Sirens', 'QTE पूरा होते ही।',
  'Eye-blur + vignette POV: बाहर police lights टिमटिमाती हैं; वह ambulance में लेटा है, "माँ… माँ…" बड़बड़ाता है; पीछे की खिड़की से accident site दूर होती जाती है; वह रोने लगता है (18+ है, फिर भी टूटकर रोता है)। Blur धीरे-धीरे बढ़ता है → fade out।',
  'हल्का: mouse/stick से धुंधला सिर इधर-उधर घुमाकर देख सकता है (POV look-around); आगे नहीं बढ़ा सकता।')
M('0.0c', 'Waking Up', '0.0b के बाद, fade in।',
  'आँख खुलती है (blur → साफ़)। Nurse/doctor: "उठो मत… तुम बिल्कुल ठीक हो।" वह: "मेरी मम्मी-पापा कहाँ हैं?" Doctor: "बेटे, हम उन्हें नहीं बचा पाए… वो आग में बहुत बुरी तरह झुलस गए थे।" वह रो पड़ता है।',
  'आँख खोलने के लिए एक button (blink prompt); "मम्मी-पापा कहाँ हैं?" पूछना एक choice-free dialogue button; बाक़ी cutscene।')
M('0.1', 'A New Start', '0.0c के बाद (कुछ दिन बाद)।',
  'वह Mall Road के किनारे आता है — अकेला।',
  'Basic tutorial: चलना, दौड़ना, camera, map, phone। Objective: "Lake side जाओ" (marker)।')
M('0.2', 'A Roof Over My Head', '0.1 में lake side पहुँचते ही।',
  'एक अनजान uncle (Mysterious Man) मिलता है। वह अपनी पूरी बात बताने को उठता है, तभी uncle: "मुझे सब पता है… कोई बात नहीं। तुम मेरे घर में रह सकते हो, मैं अक्सर घर के बाहर रहता हूँ। आओ घर चलो।"',
  'Uncle के पीछे-पीछे चलना (follow, दूरी रखनी है) Zoo Road के घर तक; घर में घूमना, बिस्तर पर सोकर save (tutorial)। Landlord की line: "आपकी बात तो पहले ही हो चुकी थी"; पलटकर देखो — uncle गायब। फिर उसकी अपनी कहानी (Chapter 1) शुरू।',
  reward='घर unlock (sleep/save)।')

H('Chapter 1 — A New Life', 2)
M('1.1', 'Rooms for Tourists', 'Prologue के बाद; repeatable।',
  'पहली बार hotel वाले से छोटी बातचीत (कमीशन कैसे मिलेगा)।',
  'Mall Road / bus stand पर tourist ढूँढना, उसकी ज़रूरत चुनना (budget / view / परिवार), सही hotel तक ले जाना।',
  'ग़लत hotel या customer नाराज़ → कमीशन नहीं।', 'कमीशन (₹)।')
M('1.2', 'Wheels for Hire', '1.1 के बाद; repeatable।',
  'Rental वाले भैया से मुलाक़ात।',
  'Customer की ज़रूरत (सस्ती / अच्छी / 1-2 दिन) से scooty चुनना, documents check, rental दिलवाना।',
  'Documents न हों और फिर भी दे दिया → जुर्माना।', 'कमीशन।')
M('1.3', 'Paperwork', 'Rental के 2-3 काम के बाद।',
  'Rental वाला पूछता है "DL बना है?" — मदद का वादा।',
  'RTO/office जाना, fees भरना (पैसे कटेंगे), form।', reward='Car test unlock।')
M('1.4', 'The Car Test', '1.3 के बाद।',
  'Examiner car में बैठता है, rules बताता है।',
  'Bus Stand → Khurpatal, traffic में। Timer + touch meter on-screen।',
  'एक भी touch / damage या time > baseline × 1.5 → fail, दोबारा fees देकर retry।', 'Car permission।')
M('1.5', 'The Scooty Test', '1.4 pass के बाद।',
  'वही examiner, अब scooty।', 'वही route, वही rules।', 'वही।', 'DL मिला — cutscene: DL हाथ में, rental वाला खुश।')

H('Chapter 2 — Learn the City', 2)
M('2.1', 'Sightseeing Rounds', 'DL के बाद; repeatable।',
  'Owner से car मिलना (एक बार)।',
  'Mall Road पर tourist से बात, 6-point या 12-point package चुनवाना, fuel check → petrol pump पर भरवाना, हर point पर रुकना (tourist उतरता/चढ़ता है), वापस छोड़ना।',
  'Car damage / tourist का समय पूरा → कम पैसे।', 'Package के हिसाब से commission − petrol खर्च।')
M('2.2', 'Fun at Tower 360', 'DL के बाद; अपनी मर्ज़ी से।',
  'हर activity का पहला intro छोटा।',
  'Go-karting race (laps, time), bumper cars, drone recording (tourist के लिए aerial video, restricted zones से बचना)।', reward='छोटे पैसे + मज़ा।')
M('2.G', 'Saving Up', 'कुल ₹20,000–30,000 (exact pending)।', '—', 'Repeatable jobs से पैसा जोड़ना।', reward='Chapter 3 unlock।')

H('Chapter 3 — The Boat Rescue', 2)
M('3.1', 'Bad Blood at the Ghat', 'Gate पूरा होते ही, Mallital ghat पर।',
  'Boat operator player पर ताना मारता है, दोस्त से उल्टा-सीधा बोलता है (player सुन लेता है)।',
  'Ghat पर tourists के साथ काम करते हुए 1-2 बार उससे टकराव (dialogue choices: चुप रहना / जवाब देना)।')
M('3.2', 'Man Overboard', '3.1 के अगले दिन।',
  'Boat पर customers, उसका पैर फिसलता है, पानी में गिरता है।',
  'Player दौड़कर कूदता है, तैरकर उसे पकड़ना (time limit), किनारे / boat तक लाना।',
  'Time खत्म → retry।', 'Cutscene: वह शुक्रिया कहता है — दोस्ती शुरू। "दोस्त" contact phone में।')

H('Chapter 4 — New Friend, New Nights', 2)
M('4.1', 'Night Ride', 'Chapter 3 के बाद।', 'शुरू और अंत में छोटी बातचीत।', 'रात में scooty चलाना, दोस्त पीछे बैठकर रास्ता बताता है।')
M('4.2', 'Maggi with a View', '4.1 के बाद।', 'Maggi खाते हुए personal बातें (माता-पिता का ज़िक्र)।', 'Viewpoint तक drive, stall पर order।')
M('4.3', 'Rooftop Nights', '4.2 के बाद।', 'Music, दोस्त की masti।', 'Mall Road cafe तक जाना, छोटा mini-game (dance / order)।')
M('4.4', 'Back Lanes', '4.3 के बाद।', '—', 'गलियाँ, local spots; दोस्त के बताए 3-4 जगह पहुँचना।')
M('4.5', 'Bus Day', '4.4 के बाद।', 'Bus में मज़ाक, नोकझोंक।', 'Bus stop तक जाना, passenger बनकर बैठना (game में पहले से), किसी जगह उतरकर घूमना।')
M('4.6', 'His Girl', '4.5 के बाद।',
  'दोस्त बताता है उसकी girlfriend बहुत प्यार करती है, कभी नहीं छोड़ेगी। "तेरी कभी girlfriend बनी है?" — running joke पहली बार।', 'Ghat पर बैठकर बात — छोटा।')

H('Chapter 5 — The Khurpatal Trip', 2)
M('5.1', 'Khurpatal Getaway', 'Chapter 4 के बाद।',
  'Bus में girlfriend का player पर ज़्यादा ध्यान (2-3 छोटे moments), player असहज।',
  'Khurpatal पर घूमना; girlfriend के messages पर dialogue choices (टालना / साफ़ मना करना) — कहानी नहीं बदलती, सिर्फ़ player का स्वभाव।')
M('5.2', 'Caught', '5.1 के कुछ दिन बाद।',
  'दोस्त girlfriend के phone में chats देखता है (player वहाँ नहीं)। दोस्त का बदलता बर्ताव।',
  'Normal काम के दौरान दोस्त के ठंडे जवाब।')

H('Chapter 6 — The Break', 2)
M('6.1', 'Breaking Point', '5.2 के बाद, रात।',
  'नशे में दोस्त, आरोप ("आज तूने अपनी औकात दिखा दी"), girlfriend बीच में, player का जवाब।',
  '1-2 मिनट की fight (player सिर्फ़ बचाव / धक्का — दोस्त को चोट न पहुँचाना objective)।')
M('6.2', 'The Edge', '6.1 के तुरंत बाद।',
  'पूरा cutscene: player खाई की तरफ़ चलता है, माता-पिता की यादें, board देखकर रुकता है; uncle: "अबे ओ! क्या कर रहा है यहाँ? रात हो गई है…" — ज़िंदगी पर छोटी बात, फिर चला जाता है। (helpline card नहीं)',
  'नहीं — सिर्फ़ cutscene।', reward='Girlfriend town छोड़ देती है (बाद में दोस्त बताएगा)।')
M('6.3–6.7', 'Picking Up the Pieces (5 missions)', '6.2 के बाद।',
  'हर mission में दोस्त की ex की शिकायतें; running joke दोबारा।',
  'साथ में taxi/boat काम, धीरे-धीरे बातचीत normal। (exact list pending)')

H('Chapter 7 — A Different Feeling', 2)
M('7.1', 'First Sight', '6.x के बीच/बाद।',
  'दोस्त breakup की बातें, player: "प्यार-व्यार सब फालतू"; दोस्त: "तेरी कभी girlfriend बनी है?" (running joke)। लड़की दिखने पर मुस्कान, दोस्त का "बेटे, हम भी गए थे, अब तुम भी जाओगे!"',
  'Binocular view: player खुद binoculars घुमाकर देखता है — पेड़ के पास लड़की।')
M('7.2', 'Lost in Thought', '7.1 के बाद।',
  'घर में: phone उठाना-रखना, माता-पिता की photo से माफ़ी; खिड़की पर uncle (सिर्फ़ player को): "तेरे मम्मी-पापा कभी नहीं चाहेंगे…"। मुड़कर देखा — गायब। "अंकल तो ऐसे ही गायब हो जाते हैं आजकल!"',
  'घर में घूमना, photo उठाना (interaction)।')

H('Chapter 8 — Two Families, Two Destinations', 2)
M('8.1', 'Two Families, Two Destinations', '7.2 के बाद।',
  'दो families, दोस्त (off-season cab) Khurpatal लेगा; player Himalaya Darshan माँगता है — दोस्त: "अच्छा, सब समझ रहा हूँ भाई।"',
  'Family को Himalaya Darshan drive करना; वहाँ family के सवाल (dialogue)।')
M('8.2', 'The Illusion', '8.1 पर पहुँचकर।',
  'Uncle सामने: "क्या देख लिया, लल्ला?… कुछ लोग real में नहीं होते, सिर्फ़ छलावे होते हैं।" — जानबूझकर भटकाने वाला hint (लड़की असली है)।',
  'Binoculars लगाना/हटाना 2-3 बार: binocular में वही लड़की, नंगी आँख से कोई और। तीसरी बार हटाते ही uncle।',
  reward='Family को वापस छोड़ना — पैसा।')

H('Chapter 9 — Curiosity', 2)
M('9.1', 'Second Glimpse', '8.2 के बाद।',
  '"Pahadi Rasoi" (Kaladhungi Road, Woodland के सामने) पर खाना; लड़की ऊपर waterfall के पास। दोस्त: "ओ भाई, औकात में रह ले…"',
  'Phone camera उठाने की कोशिश — दोस्त रोकता है (choice: photo न लेना)।')
M('9.2', 'Shadowing', '9.1 के तुरंत बाद।',
  'लड़का साथ दिखता है; "मेरी वाली धोखा दे गई, तेरी वाली बिना बने ही!"; फिर लड़का दूसरी लड़की के साथ — राहत। (Player को नहीं पता: लड़की ने उसे देख लिया है।)',
  'दूर से follow करना — detection meter; बहुत पास गए / सामने आ गए तो लड़की मुड़ती है → retry। Waterfall steps से नीचे gate तक, फिर उनकी गाड़ी के पीछे Tallital तक (दोस्त की गाड़ी में)।',
  'Detected → retry।')

H('Chapter 10 — "Koi Mil Gaya" Moment', 2)
M('10.1', 'The Address', '9.2 के बाद।',
  'लड़की किसी और से villa का address पूछती है — जानबूझकर player के सामने। दोस्त: "तू पूछने दे…"। दोस्त की ride आ जाती है, गाड़ी देने से मना; "कोई मिल गया देख के आया है क्या?"',
  'Player लड़की के पास जाकर बात करने का choice; plan: bus से Capitol।')
M('10.2', 'Ride to Capitol', '10.1 के बाद।',
  'Bus में चुप्पी, एक-दो awkward बातें।', 'Bus stop तक साथ चलना, ticket लेना, bus में बैठना, Mallital पर उतरना, Capitol Cinema तक पैदल।')
M('10.3', 'The Missing Cab', 'Capitol पहुँचकर।',
  'Cab कोई और driver ले गया; player की माफ़ी; लड़की: "मैं यहीं पैदा हुई हूँ… मुझे पता था। मुझे तुम्हारे साथ time spend करना था।" (spy mode में उसने उसे पकड़ लिया था — वह भी बताती है)',
  'खाली parking तक दौड़कर cab ढूँढना (छोटा)।')
M('10.4', 'Coffee for Two', '10.3 के बाद (दोस्त साथ नहीं)।',
  '"चुप हो जाओ, वरना यहीं छोड़ दूँगी" … पहली सही बातचीत।', 'Order चुनना; dialogue choices (nervous / honest / मज़ाक) — next step: पूरा dialogue लिखना।')


H('E. Analysis — क्या हटाएँ, क्या जोड़ें, क्या ज़रूरी नहीं')
P('हटाने / merge करने लायक', True)
for x in [
    '1.1 + 1.2 को एक "Odd Jobs" loop बना दें — दोनों same gameplay हैं (customer की ज़रूरत → सही जगह)। दो अलग mission ज़रूरी नहीं।',
    '1.3 Paperwork को cutscene + एक pay button बना दें; office तक चलना boring filler है।',
    '1.5 Scooty test: same route दोबारा 12 km — repetitive। Scooty test छोटा रखें (Bus Stand → Barapathar) या 1.4 के साथ एक ही test में दोनों license।',
    '4.4 Back Lanes और 4.1 Night Ride लगभग same (scooty घूमना) — एक हटाएँ या merge करें।',
    '6.3–6.7 के 5 normal missions बहुत लंबा gap है emotional peak के बाद — 2-3 काफ़ी हैं, और उनमें से एक 7.1 ही हो।',
    '7.2 Lost in Thought — छोटा cutscene काफ़ी है, अलग mission नहीं।',
]: L(x)
P('जोड़ने लायक', True)
for x in [
    'Chapter 1 में Uncle का एक छोटा "दिखना" (crowd में दिखे, फिर गायब) — वरना वह Ch 6 तक ग़ायब रहता है और player भूल जाता है।',
    'Ch 3 से पहले boatman का setup: 1-2 बार ghat पर उसका ताना free-roam में ही (random encounter), ताकि jealousy पहले से दिखे।',
    'Girlfriend का introduction 4.6 से पहले: Ch 4 के किसी bonding mission में वह थोड़ी देर साथ आए — Ch 5 में अचानक नहीं लगेगी।',
    'Girl का भी एक छोटा पल 9.2 में: वह मुड़कर हल्का मुस्कुराए (player न देखे, सिर्फ़ camera दिखाए) — 10.3 का reveal earned लगेगा।',
    'Opening में बचपन/परिवार का 1 छोटा playable moment (car में बैठकर window से बाहर देखना, mummy से बात) — ताकि accident का दर्द महसूस हो।',
    'Game note: Uncle + मम्मी की line "पहचान नहीं पाओगे" का payoff ending में ज़रूर रखें।',
]: L(x)
P('ज़रूरी नहीं / risky', True)
for x in [
    '2.G का exact ₹ amount grind न बने — अगर 20-30k ज़्यादा लगे तो 10-15k।',
    '2.2 drone recording — mechanic भारी है (flight + zones); later DLC जैसा रख सकते हैं।',
    '8.1 में family की बातचीत लंबी न हो — 2-3 lines।',
    'DL test का zero-touch rule आपने रखा है — बस traffic AI stable होना चाहिए, वरना unfair fail होंगे।',
]: L(x)

H('C. Continuity rules (updated)')
for x in [
    'Boat operator की girlfriend और Boss की बेटी — दो अलग लड़कियाँ।',
    'Uncle हर scene में सिर्फ़ player को दिखता है (घर, खिड़की, Lovers Point, Himalaya Darshan)।',
    '"छलावा" hint लड़की के असली होने पर शक डालता है — पर वह असली है।',
    'Tallital पर गाड़ी दोस्त की है; Capitol तक bus; coffee में दोस्त नहीं।',
    'पहली और दूसरी झलक में कोई बातचीत नहीं; लड़की ने spy mode में player को देख लिया था।',
    'Woodland: player-दोस्त Kaladhungi Road के restaurant पर; लड़की ऊपर waterfall पर।',
    'Boss की बेटी होने का reveal अभी नहीं।',
]: L(x)

H('D. अब भी pending')
for x in [
    'सबके नाम (player, दोस्त, girlfriend, लड़की, uncle, Boss)।',
    'Commission amounts, earning gate की exact रकम, 6/12-point के points।',
    'Breakup के बाद 5 missions की exact list।',
    'लड़की की personality, उम्र, background; coffee के बाद अगली मुलाक़ात।',
    'Boss reveal कब / कैसे; kidnapping arc (future)।',
    'Tower 360 go-karting / bumper cars के reference photos।',
    'Game note: अभी mission system नहीं है (free-roam batch पहले) — ऊपर सब plan है।',
]: L(x)

out = r'C:\Users\Lenovo\Downloads\Nainital_Story_Mission_Bible_v3.docx'
d.save(out); print(out)

