<?php

trait T {
  public function bar() {
    return $this instanceof C;
  }
}

class C {
  use T;
}

print_r((new C)->bar());
