Diagnostics used on 20 Nov 2026 (docs/VEHICLES.md section 9). Run headless:
  blender -b <file.blend> --python-exit-code 1 -P <script.py> [-- <out.png>]
front_check.py  (open a blend, then: -P front_check.py -- out.png) ortho render of the whole row from the front-left
anim_check.py   every object with an action: its curves and keys (Auto Keying leftovers, the bus door animations)
neg_scale.py    objects with a negative scale / determinant per collection (the owner's mirrors)
mirror_check.py per vehicle: the wheels' mean x vs the body's x range, door positions
door_cmp.py     the two bus doors: world x/y range, origin, scale, keys
