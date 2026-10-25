# Native Static Constructors

We give users a way to interact with static constructors.

* They are renamed from `.cctor` to `StaticConstructor`. If this is unavailable, additional underscores are added.
* Like other methods, they are publicized.
* The `specialname` and `rtspecialname` attributes are removed.
