<?php
interface I
{
    public function foo();
}
abstract class A implements I
{
    public abstract function foo();
}
class B extends A
{
    public function foo() {
        return __METHOD__;
    }
}
echo (new B)->foo();

echo "Done.";
