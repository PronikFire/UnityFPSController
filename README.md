# UnityFPSController
Player controller for first person games on unity.

I wrote the script for myself but decided to share it.
I couldn't find any normal, free similar controllers (although I didn't try very hard).

I added some comments to make it clearer for those who want to figure it out, but my variables are self-explanatory.
The controller has two modes of operation: through rigidbody.velocity or through transform.position. How to switch it is written at the top of the file.

# Important! 
According to my experiments, it works best in Simulation mode Update and if you are in transform.position mode with Collision Detection Discrete switched on.
