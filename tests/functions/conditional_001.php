<?php

if (true) {
	function foo($a) {
		echo "foo $a 1";
	}
}
else {
	function foo($a) {
		echo "foo $a 2";
	}
}

foo("xxx");

echo "Done.";
