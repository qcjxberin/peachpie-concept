<?php

class X
{
    private $ppp = 123;

    function foo()
    {
        return function () { echo $this->ppp; };
    }
}

(new X)->foo()();

echo "Done.";
