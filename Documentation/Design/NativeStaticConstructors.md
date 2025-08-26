# Native Static Constructors

It would be desirable to offer users a way to interact with static constructors.

* They should be renamed from `.cctor` to `_cctor`. If this is unavailable, additional underscores should be added to the beginning.
* Like other methods, they should be publicized.
* The `specialname` and `rtspecialname` attributes should be removed.
